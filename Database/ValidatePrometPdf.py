import json
import sys
import xml.etree.ElementTree as ET
from decimal import Decimal
from pathlib import Path
from pypdf import PdfReader
import pypdfium2 as pdfium

work=Path(sys.argv[1]);pdf_path=Path(sys.argv[2]);reports=work/'reports'
cases=json.loads((work/'scenario_saved.json').read_text(encoding='utf-8-sig'),parse_float=Decimal)
checks=[]
for case in cases:
    data=ET.parse(reports/('Dokument_'+case['Number']+'.xml')).getroot()
    rows=[{child.tag:child.text for child in row} for row in data if not row.tag.startswith('{')]
    found=[r for r in rows if r.get('Ident')==str(case['Ident'])]
    assert len(found)==1,(case['Number'],case['Ident'])
    price=found[0].get('Nc',found[0].get('Cijena'))
    assert Decimal(price)==Decimal(str(case['SavedPrice'])),(case['Number'],price,case['SavedPrice'])
    checks.append('Printed price '+case['Number']+'/'+str(case['Ident']))
    if case['Status']=='V':
        text=' '.join(p.extract_text() for p in PdfReader(reports/('Finansije_'+case['Number']+'.pdf')).pages)
        amount=f"-{abs(case['SavedValue']):.2f}".replace('.',',')
        assert text.count(amount)>=4,(case['Number'],amount,text)
        checks.append('Return financial signed lines/totals '+case['Number'])
text=' '.join(p.extract_text() for p in PdfReader(reports/'PregledTroskovaPoKontimaINalozimaZaSvaSkladista.pdf').pages)
assert '01.11.2026' in text and '04.11.2026' in text,text
checks.append('Expense report header equals selected period')
review=work/'visual_review';review.mkdir(exist_ok=True)
doc=pdfium.PdfDocument(str(pdf_path))
for index in [0,1]:
    doc[index].render(scale=1.5).to_pil().save(review/('final_cover_'+str(index+1)+'.png'))
doc.close()
(work/'print_price_validation.json').write_text(json.dumps({'Passed':len(checks),'Checks':checks},ensure_ascii=False,indent=2),encoding='utf-8')
print('PASS',len(checks),'actual printed prices, signed financial returns and expense period checks')

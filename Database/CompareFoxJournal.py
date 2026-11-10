"""Compare native Fox expressions with actual RDLC expression output cells."""
import json,sys,zipfile
from decimal import Decimal
from pathlib import Path
import xml.etree.ElementTree as ET
work=Path(sys.argv[1]);folder=work/'report_hotfix'
ns={'x':'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}
def sheet_rows(file):
    with zipfile.ZipFile(file) as z:
        strings=[]
        if 'xl/sharedStrings.xml' in z.namelist():
            strings=[''.join(t.text or '' for t in item.findall('.//x:t',ns)) for item in ET.fromstring(z.read('xl/sharedStrings.xml'))]
        result=[]
        for row in ET.fromstring(z.read('xl/worksheets/sheet1.xml')).findall('.//x:sheetData/x:row',ns):
            values=[]
            for cell in row:
                value=cell.findtext('x:v','',ns)
                if cell.get('t')=='s':value=strings[int(value)]
                elif cell.get('t')=='inlineStr':value=''.join(t.text or '' for t in cell.findall('.//x:t',ns))
                elif value and cell.get('t') not in ['str','inlineStr','b','e']:value=Decimal(value)
                values.append(value)
            result.append(values)
        return result
fox=json.loads((work/'fox_dnevnik_values.json').read_text(encoding='utf-8-sig'),parse_float=Decimal)
groups={}
checks=[]
for expected in fox:
    key=(expected['Material'],expected['Warehouse'])
    if key not in groups:groups[key]=sheet_rows(folder/('Dnevnik_'+str(key[0])+'_'+str(key[1])+'.xlsx'))
    candidates=[row for row in groups[key] if expected['Number'] in row]
    assert len(candidates)==1,(key,expected['Number'],candidates,groups[key][:16])
    numbers=[value for value in candidates[0] if isinstance(value,Decimal)]
    actual=numbers[-5:]
    target=[Decimal(str(expected[k])) for k in ['IncomingQuantity','OutgoingQuantity','Price','Debit','Credit']]
    assert actual==target,(key,expected['Number'],actual,target,candidates[0])
    checks.append({'Material':key[0],'Warehouse':key[1],'Document':expected['Number'],'Status':expected['Status'],'Values':[str(x) for x in actual]})
result={'Rows':len(checks),'NumericFieldsCompared':len(checks)*5,'Failures':0,'RowsCompared':checks}
(work/'fox_dnevnik_comparison.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print('PASS native Fox / rendered RDLC:',len(checks),'rows,',len(checks)*5,'numeric columns, zero differences')

import json,re,sys,zipfile
from decimal import Decimal
from pathlib import Path
import xml.etree.ElementTree as E

work=Path(sys.argv[1]); fox=json.loads((work/'fox_inventory_balances.json').read_text(encoding='utf-8-sig'),parse_float=Decimal)
ns={'x':'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}
def sheet(file):
    with zipfile.ZipFile(file) as z:
        strings=[]
        if 'xl/sharedStrings.xml' in z.namelist():strings=[''.join(t.text or '' for t in item.findall('.//x:t',ns)) for item in E.fromstring(z.read('xl/sharedStrings.xml'))]
        result=[]
        for row in E.fromstring(z.read('xl/worksheets/sheet1.xml')).findall('.//x:sheetData/x:row',ns):
            values=[]
            for cell in row:
                value=cell.findtext('x:v','',ns)
                if cell.get('t')=='s':value=strings[int(value)]
                elif cell.get('t')=='inlineStr':value=''.join(t.text or '' for t in cell.findall('.//x:t',ns))
                elif value and cell.get('t') not in ['str','b','e']:value=Decimal(value)
                values.append(value)
            result.append(values)
        return result

results=[]
for warehouse in [1000,1001,1002,1003]:
    expected={int(r['Material']):r for r in fox if int(r['Warehouse'])==warehouse}
    xml=E.parse(work/'wpf_costs'/f'InventurneZalihe_{warehouse}.xml').getroot()
    actual={int(r.findtext('Ident')):{c.tag:c.text for c in r} for r in xml if not r.tag.startswith('{')}
    assert set(actual)==set(expected),(warehouse,'material membership')
    printed=sheet(work/'wpf_costs'/f'InventurneZalihe_{warehouse}.xlsx')
    body={}; headers={}; previous=None
    for row in printed:
        nums=[n for n in row if isinstance(n,Decimal)]
        if any(isinstance(v,str) and v.startswith('Naziv:') for v in row):previous=nums
        elif nums and int(nums[0]) in expected and len(nums)==5:
            ident=int(nums[0]);body[ident]=nums;headers[ident]=previous
    for ident,r in expected.items():
        a=actual[ident]
        for field,key in [('Nc','Price'),('MatKolic','Quantity'),('MatVrijed','Value'),('InvKolic','InvQuantity'),('InvVrijed','InvValue')]:
            assert Decimal(a[field])==Decimal(r[key]),(warehouse,ident,field,a[field],r[key])
        q,v,iq,iv=(Decimal(r[k]) for k in ['Quantity','Value','InvQuantity','InvValue'])
        assert Decimal(a['KolicRazlika'])==q-iq and Decimal(a['VrijedRazlika'])==v-iv,(warehouse,ident,'differences')
        assert body.get(ident)==[Decimal(ident),iq,iv,q,v],(warehouse,ident,'printed inventory and book balances',body.get(ident))
        assert headers.get(ident)==[Decimal(r['Price']),max(q-iq,0),max(iq-q,0),max(v-iv,0),max(iv-v,0)],(warehouse,ident,'printed price and differences',headers.get(ident))
    bv=sum(Decimal(r['Value']) for r in expected.values());iv=sum(Decimal(r['InvValue']) for r in expected.values())
    for a in actual.values():
        assert Decimal(a['VrijedRazlikaManjak'])==max(bv-iv,0) and Decimal(a['VrijedRazlikaVisak'])==max(iv-bv,0),(warehouse,'net monetary footer')
    # Original ASCII layout wraps wide figures; compare its unambiguous original nc fields.
    text=(work/'fox_inventory'/f'invzal_{warehouse}.txt').read_text(encoding='cp1250')
    prices=[Decimal(x) for x in re.findall(r'(?<!\d)(\d+\.\d{9})(?!\d)',text)]
    lines=text.splitlines()
    for index,line in enumerate(lines[:-1]):
        fragment=re.search(r'(?<!\d)(\d+\.\d{8})(?!\d)',line)
        if fragment:
            tail=re.search(r'(?<!\d)(\d)\s*$',lines[index+1])
            assert tail,(warehouse,'wrapped original nc field',line)
            prices.append(Decimal(fragment[1]+tail[1]))
    assert sorted(prices)==sorted(Decimal(r['Price']) for r in expected.values()),(warehouse,'original FRX printed prices')
    results.append({'Warehouse':warehouse,'Materials':len(expected),'BookValue':str(bv),'InventoryValue':str(iv),'WpfDataAndPrintedValuesMatchFox':True})
    print('PASS inventory balances',warehouse,len(expected),'materials; book value',bv,'inventory value',iv)
(work/'inventory_balance_comparison.json').write_text(json.dumps({'Warehouses':results,'Failures':0},ensure_ascii=False,indent=2),encoding='utf-8')

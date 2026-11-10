"""Compare WPF results to inspected Fox source rules, without executing Fox."""
import json,re,sys,zipfile
from decimal import Decimal as D, ROUND_HALF_UP
from pathlib import Path
import xml.etree.ElementTree as E

work=Path(sys.argv[1]);reports=work/'reports'
cases=json.loads((reports/'financial_cases.json').read_text(encoding='utf-8-sig'),parse_float=D)
ns={'x':'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}
def money(x):return D(str(x or 0)).quantize(D('.01'),rounding=ROUND_HALF_UP)
def normalized(name):return re.sub(r'\s+','',(work/'fox_source'/(name+'_methods.txt')).read_text(encoding='utf-8')).lower()
for name in ['nalogweb','nalogiweb','nalogiwebp','nalogmweb']:
    code=normalized(name)
    assert 'replacedatdokstwithadatun' in code,(name,'original posting-date rule')
    if name in ['nalogiweb','nalogiwebp']:
        assert "aanalst='1lt01'" in code and 'amjt=substr(amjtst,2,5)' in code,(name,'original Oracle mapping')
assert "aaa='fakt.dobav.'+alltrim(brdok)+''+aanalst" in normalized('nalogweb')
assert "bbb='magacin'+alltrim(analog)" in normalized('nalogweb')
assert "bbb=alltrim(brdok)+'ncvumag'" in normalized('nalogiwebp')

def sheet(file):
    with zipfile.ZipFile(file) as z:
        strings=[]
        if 'xl/sharedStrings.xml' in z.namelist():strings=[''.join(t.text or '' for t in item.findall('.//x:t',ns)) for item in E.fromstring(z.read('xl/sharedStrings.xml'))]
        result=[]
        for row in E.fromstring(z.read('xl/worksheets/sheet1.xml')).findall('.//x:sheetData/x:row',ns):
            values=[]
            for cell in row:
                v=cell.findtext('x:v','',ns)
                if cell.get('t')=='s':v=strings[int(v)]
                elif cell.get('t')=='inlineStr':v=''.join(t.text or '' for t in cell.findall('.//x:t',ns))
                elif v and cell.get('t') not in ['str','b','e']:v=D(v)
                values.append(v)
            result.append(values)
        return result

results=[];checked=0
for case in cases:
    expected=[];status=case['Status']
    def add(row,account,debit,credit,description,partner='',unit=None,cost=None):
        expected.append({'LineNumber':len(expected)+1,'Sintstav':account,'Brdokst':description,'Datnsta':row['Datun'],'Datdokst':row['Datun'],'Analst':partner,'Ourst':unit,'Mjtst':cost,'Dug1st':money(debit),'Pot1st':money(credit),'Sifakt':case['Activity'],'Bronsta':case['Journal'],'Brfak':case['Number']})
    for r in case['Source']:
        value=D(r['Vrijed']);doc=(r['Brdok'] or '').strip()
        if status=='U':
            vendor=(r['Analst'] or '').strip();description=('FAKT.DOBAV.'+doc+'  '+vendor).rstrip()
            if value:
                add(r,4320000,0,value,description,vendor);add(r,r['Kontosklad'],value,0,'MAGACIN '+case['Number'])
            for account,key in [(1000000,'Fvrijed'),(1001400,'Trospe'),(1002000,'Cartro'),(1003000,'Porppp'),(1004000,'Zavtro')]:
                amount=money(r[key])
                if amount:add(r,account,amount,0,description);add(r,account,0,amount,description)
        elif status in ['I','V']:
            amount=-value
            if status=='I' and amount<=0 or status=='V' and amount>=0:continue
            add(r,r['Konto1'],amount,0,doc,unit=r['Ourst'],cost=r['Mjtst']);add(r,r['Kontosklad'],0,amount,doc+' NCV U MAG',unit=0,cost=0)
        elif status=='M' and D(r['Kolic'])<0 and value<0:
            add(r,r['Kontosklad1'],-value,0,doc);add(r,r['Kontosklad'],0,-value,doc+' NCV U MAG')
    assert case['Lines']==expected,(case['Number'],'Fox local journal rules',[(a,b) for a,b in zip(case['Lines'],expected) if a!=b][:2])
    mapped=[]
    for r in expected:
        cost=r['Mjtst']
        if status in ['I','V'] and cost is not None:cost=int(str(cost).zfill(6)[1:6])
        mapped.append({'Sintstav':r['Sintstav'],'Brdokst':r['Brdokst'].strip(),'Datdokst':r['Datdokst'],'Analst':r['Analst'] if status=='U' else '1LT01','Mjtst':cost,'Dug1st':r['Dug1st'],'Pot1st':r['Pot1st']})
    assert case['OracleMapping']==mapped,(case['Number'],'Oracle payload rules (offline only)')
    rows=sheet(reports/(case['Number']+'.xlsx'));printed=[]
    for row in rows:
        numbers=[v for v in row if isinstance(v,D)]
        if len(numbers)==3 and numbers[0]>=1000000:printed.append(row)
    assert len(printed)==len(expected),(case['Number'],'printed line count')
    for actual,wanted in zip(printed,expected):
        numbers=[v for v in actual if isinstance(v,D)]
        assert numbers==[D(wanted['Sintstav']),wanted['Dug1st'],wanted['Pot1st']],(case['Number'],'printed accounts/amounts',numbers,wanted)
        assert wanted['Brdokst'] in actual and (not wanted['Analst'] or wanted['Analst'] in actual),(case['Number'],'printed description/partner',actual)
        assert '05.11.2026' in actual,(case['Number'],'printed date')
    all_values=[v for row in rows for v in row]
    assert str(case['Journal']).zfill(4) in all_values,(case['Number'],'four-digit journal number')
    debit=sum(r['Dug1st'] for r in expected);credit=sum(r['Pot1st'] for r in expected)
    assert debit==credit,(case['Number'],'balance')
    results.append({'Status':status,'Calculation':case['Number'],'Journal':str(case['Journal']).zfill(4),'Lines':len(expected),'Debit':str(debit),'Credit':str(credit),'FoxSourceRulesMatch':True,'ActualPrintedCellsMatch':True,'OfflineOracleMappingMatches':True})
    checked+=len(expected)
    print('PASS',case['Number'],status,len(expected),'Fox-equivalent lines; debit = credit =',debit)
(work/'financial_comparison.json').write_text(json.dumps({'Documents':results,'LinesChecked':checked,'Failures':0,'FoxWasExecuted':False,'OracleConnectionsUsed':False},ensure_ascii=False,indent=2),encoding='utf-8')

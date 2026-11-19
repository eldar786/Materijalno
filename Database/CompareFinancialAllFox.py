"""Independent comparison of actual SQL/TESTPDB journals to read-only Fox form rules."""
from pathlib import Path
from decimal import Decimal as D, ROUND_HALF_UP
import json,re,sys

w=Path(sys.argv[1])
cases=json.loads((w/'cases_snapshot.json').read_text(encoding='utf-8-sig'),parse_float=D)
def money(value):return D(str(value or 0)).quantize(D('.01'),rounding=ROUND_HALF_UP)
def amount(value):
    s=str(value or '0').strip()
    return D(s.replace('.','').replace(',','.')) if ',' in s else D(s or '0')
def code(name):
    lines=(w/'fox_reference'/(name+'_methods.txt')).read_text(encoding='utf-8-sig').splitlines()
    return re.sub(r'\s+','', '\n'.join(s for s in lines if not s.lstrip().startswith('*'))).lower()
for form in ('nalogweb','nalogiweb','nalogiwebp','nalogmweb'):
    assert 'replacedatdokstwithadatun' in code(form),form
assert "amjt='00000'" in code('nalogmweb')
assert "aanalst='1lt01'" in code('nalogiweb')
assert "aanalst='1lt01'" in code('nalogiwebp')
assert 'amjt=substr(amjtst,2,5)' in code('nalogiweb')
results=[];line_count=0;field_count=0
for case in cases:
    expected=[];status=case['Status']
    def add(row,account,debit,credit,description,partner='',unit=None,cost=None):
        expected.append(dict(LineNumber=len(expected)+1,Sintstav=account,Brdokst=description,Datnsta=row['Datun'],
            Datdokst=row['Datun'],Analst=partner,Ourst=unit,Mjtst=cost,Dug1st=money(debit),Pot1st=money(credit),
            Sifakt=case['Activity'],Bronsta=case['Journal'],Brfak=case['Number']))
    for row in case['Source']:
        value=D(row['Vrijed']);doc=(row['Brdok'] or '').strip()
        if status=='U':
            supplier=(row['Analst'] or '').strip()
            description=('FAKT.DOBAV.'+doc+'  '+supplier).rstrip()
            if value:
                add(row,4320000,0,value,description,supplier)
                add(row,row['Kontosklad'],value,0,'MAGACIN '+case['Number'])
            for account,key in ((1000000,'Fvrijed'),(1001400,'Trospe'),(1002000,'Cartro'),(1003000,'Porppp'),(1004000,'Zavtro')):
                a=money(amount(row[key]))
                if a:add(row,account,a,0,description);add(row,account,0,a,description)
        elif status in ('I','V'):
            a=-value
            if status=='I' and a<=0 or status=='V' and a>=0:continue
            add(row,row['Konto1'],a,0,doc,unit=row['Ourst'],cost=row['Mjtst'])
            add(row,row['Kontosklad'],0,a,doc+' NCV U MAG',unit=0,cost=0)
        elif status=='M' and D(row['Kolic'])<0 and value<0:
            add(row,row['Kontosklad1'],-value,0,doc)
            add(row,row['Kontosklad'],0,-value,doc+' NCV U MAG')
    assert len(expected)==len(case['Lines']),case['Number']
    for ix,(want,got,remote) in enumerate(zip(expected,case['Lines'],case['Oracle']),1):
        assert got==want,(case['Number'],ix,'SQL differs from Fox',want,got)
        field_count+=len(want)
        if status=='M':cost='00000'
        elif status in ('I','V') and want['Mjtst'] is not None:
            cost=str(int(str(want['Mjtst']).zfill(6)[1:6])).zfill(5)
        elif want['Mjtst'] is not None:cost=str(want['Mjtst']).zfill(5)
        else:cost=''
        oracle_expected=dict(Konto=str(want['Sintstav']),Description=want['Brdokst'].strip(),Date=want['Datdokst'],
            Partner=want['Analst'].strip() if status=='U' else '1LT01',CostCenter=cost,Debit=want['Dug1st'],
            Credit=want['Pot1st'],Author='MAT',Line=ix,Nalog=remote['Nalog'])
        assert remote==oracle_expected,(case['Number'],ix,'Oracle differs',oracle_expected,remote)
        field_count+=len(oracle_expected)
    debit=sum(row['Dug1st'] for row in expected);credit=sum(row['Pot1st'] for row in expected)
    assert debit==credit
    line_count+=len(expected)
    results.append(dict(Status=status,Calculation=case['Number'],Journal=str(case['Journal']).zfill(4),
        Lines=len(expected),Debit=str(debit),Credit=str(credit),SQLMatchesFoxRules=True,
        OracleValuesMatch=True,Note='U Oracle export is the requested application extension; original nalogweb contains local journal generation.' if status=='U' else 'Oracle mapping checked against the corresponding Fox web form.'))
    print('PASS',case['Number'],status,len(expected),'actual SQL/Oracle lines; D=P='+str(debit))
(w/'fox_comparison.json').write_text(json.dumps(dict(Passed=True,Documents=results,Lines=line_count,
    FieldsChecked=field_count,FoxExecuted=False,FoxModified=False),ensure_ascii=False,indent=2),encoding='utf-8')
print('ALL PASSED; documents='+str(len(cases))+'; lines='+str(line_count)+'; independently checked fields='+str(field_count))

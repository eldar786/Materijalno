import json,re,sys,zipfile
from decimal import Decimal
from collections import defaultdict
from pathlib import Path
import xml.etree.ElementTree as E

work=Path(sys.argv[1]); native=json.loads((work/'fox_cost_movements.json').read_text(encoding='utf-8-sig'),parse_float=Decimal)
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
for period in ['Godina','PrviKvartal','TreciKvartal']:
    movements=[r for r in native if r['Range']==period]
    warehouses=sorted({int(r['Warehouse']) for r in movements})
    for bydoc in [False,True]:
        name=('PoKontimaINalozima_' if bydoc else 'PoKontima_')+period
        groups=defaultdict(list)
        for r in movements:
            groups[(0 if bydoc else int(r['Warehouse']),int(r['MasterAccount']),r['Document'] if bydoc else '')].append(r)
        expected={k:-sum(Decimal(r['Value']) for r in v) if bydoc else abs(sum(Decimal(r['Value']) for r in v)) for k,v in groups.items()}
        account_totals={a:sum(v for (_,account,_),v in expected.items() if account==a) for a in sorted({k[1] for k in expected})}
        warehouse_totals={w:sum(abs(Decimal(r['Value'])) for r in movements if int(r['Warehouse'])==w) for w in warehouses}
        grand=sum((-Decimal(r['Value']) if bydoc else abs(Decimal(r['Value']))) for r in movements)
        xml=E.parse(work/'wpf_costs'/(name+'.xml')).getroot()
        rows=[{c.tag:c.text for c in r} for r in xml if not r.tag.startswith('{')]
        actual={(int(r['Kljnaz']),int(r['Konto1']),r.get('Brfak') or ''):Decimal(r['Vrijed']) for r in rows}
        assert actual==expected,(name,'WPF data groups',[(k,actual.get(k),v) for k,v in expected.items() if actual.get(k)!=v][:8])
        if bydoc:
            for r in rows:
                key=(0,int(r['Konto1']),r['Brfak'])
                expected_date=groups[key][-1]['DocumentDate']
                assert (r.get('DatumDokumenta') or '')[:10].replace('-','')==expected_date,(name,key,'document date')
        else:
            totals={w:sum(Decimal(r['UkupnoVrijed']) for r in rows if int(r['Kljnaz'])==w) for w in warehouses}
            assert totals==warehouse_totals,(name,'warehouse totals')
        text=(work/'fox_costs'/((('kontona1_' if bydoc else 'kontrosv_')+period)+'.txt')).read_text(encoding='cp1250')
        fox={};block={};wi=0;fox_grand=None;account=None;native_order=[]
        for line in text.splitlines():
            code=re.match(r'^\s+(\d{7})\s+',line)
            value=re.search(r'(-?[\d,]+\.\d{2})\s*$',line)
            if bydoc and code and not value:
                account=int(code[1]);continue
            if not value:continue
            amount=Decimal(value[1].replace(',',''))
            if 'UKUPNO:' in line:fox_grand=amount
            elif bydoc and 'Ukupno konto:' in line:
                assert code and amount==account_totals[int(code[1])],(name,'Fox account total',line)
            elif not bydoc and 'Ukupan iznos za skl' in line:
                w=warehouses[wi];wi+=1
                assert amount==warehouse_totals[w],(name,'Fox warehouse total',w)
                for (a,doc),v in block.items():fox[(w,a,doc)]=v
                block={}
            elif not bydoc and code:block[(int(code[1]),'')]=amount
            elif bydoc:
                doc=re.match(r'^\s+(\d+-\S+)\s+',line)
                if doc:
                    key=(0,account,doc[1]);assert key not in fox,(name,'repeated calculation',key)
                    fox[key]=amount;native_order.append(key)
        assert fox==expected,(name,'original FRX values',[(k,fox.get(k),v) for k,v in expected.items() if fox.get(k)!=v][:8])
        assert fox_grand==grand,(name,'original FRX grand total',fox_grand,grand)
        printed=sheet(work/'wpf_costs'/(name+'.xlsx'));printed_rows={};printed_totals={};printed_order=[];wh=None;account=None;printed_grand=None;headers=[]
        for values in printed:
            nums=[v for v in values if isinstance(v,Decimal)]
            strings=[v for v in values if isinstance(v,str)]
            docs=[v for v in strings if re.fullmatch(r'\d+-\S+',v)]
            if 'UKUPNO:' in strings:
                printed_grand=nums[-1];continue
            if bydoc:
                if 'Ukupno konto:' in strings:
                    printed_totals[int(nums[0])]=nums[-1]
                elif docs and nums:
                    key=(0,account,docs[0]);assert key not in printed_rows,(name,'repeated printed calculation',key)
                    printed_rows[key]=nums[-1];printed_order.append(key)
                elif len(nums)==1 and nums[0]>=1000000:
                    account=int(nums[0]);headers.append(account)
            else:
                if nums and nums[0] in warehouses and len(nums)==1:wh=int(nums[0])
                elif wh is not None and len(nums)==2 and nums[0]>=1000000:printed_rows[(wh,int(nums[0]),'')]=nums[1]
                if any('Ukupan iznos za skladište:' in v for v in strings):printed_totals[wh]=nums[-1]
        assert printed_rows==expected,(name,'actual printed XLSX cells',len(printed_rows),len(expected),[(k,printed_rows.get(k),v) for k,v in expected.items() if printed_rows.get(k)!=v][:8])
        assert printed_totals==(account_totals if bydoc else warehouse_totals),(name,'printed subtotals')
        assert printed_grand==grand,(name,'printed grand total')
        if bydoc:
            assert headers==sorted(account_totals),(name,'one account heading per code across all warehouses')
            assert printed_order==native_order,(name,'calculation order differs from original Konto index')
        results.append({'Report':name,'Groups':len(expected),'Accounts':len(account_totals),'GrandTotal':str(grand),'OriginalFoxValuesMatch':True,'PrintedWpfValuesMatch':True,'OriginalCalculationOrderMatches':bydoc,'OriginalTemplate':'kontona1.frx' if bydoc else 'kontrosv.frx'})
        print('PASS',name,len(expected),'groups;',len(account_totals),'accounts; total',grand)
(work/'cost_report_comparison.json').write_text(json.dumps({'Reports':results,'Failures':0},ensure_ascii=False,indent=2),encoding='utf-8')

import json,re,sys,zipfile
from decimal import Decimal
from pathlib import Path
import xml.etree.ElementTree as ET
work=Path(sys.argv[1]);ns={'x':'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}
def sheet_rows(file):
    with zipfile.ZipFile(file) as z:
        strings=[]
        if 'xl/sharedStrings.xml' in z.namelist():strings=[''.join(t.text or '' for t in item.findall('.//x:t',ns)) for item in ET.fromstring(z.read('xl/sharedStrings.xml'))]
        result=[]
        for row in ET.fromstring(z.read('xl/worksheets/sheet1.xml')).findall('.//x:sheetData/x:row',ns):
            values=[]
            for cell in row:
                value=cell.findtext('x:v','',ns)
                if cell.get('t')=='s':value=strings[int(value)]
                elif cell.get('t')=='inlineStr':value=''.join(t.text or '' for t in cell.findall('.//x:t',ns))
                elif value and cell.get('t') not in ['str','b','e']:value=Decimal(value)
                values.append(value)
            result.append(values)
        return result
fox=json.loads((work/'fox_inventory_slips.json').read_text(encoding='utf-8-sig'),parse_float=Decimal)
actual={};printed={};native={};checks=0
for warehouse in [1000,1001,1002]:
    root=ET.parse(work/'report_hotfix'/f'InventurniListici_{warehouse}.xml').getroot()
    actual[warehouse]={int(row.findtext('Ident')):{child.tag:child.text for child in row} for row in root if not row.tag.startswith('{')}
    printed[warehouse]=sheet_rows(work/'report_hotfix'/f'InventurniListici_{warehouse}.xlsx')
    text=(work/'inventory_reference'/f'invl_{warehouse}.txt').read_text(encoding='cp1250')
    native[warehouse]={int(m[1]):Decimal(m[2].replace(',','')) for m in re.finditer(r'^\s+(\d+)\s+.*?(-?[\d,]+\.\d{9})\s*$',text,re.M)}
    assert set(actual[warehouse])==set(native[warehouse]),('Material list mismatch',warehouse,set(actual[warehouse])^set(native[warehouse]))
for expected in fox:
    warehouse=int(expected['Warehouse']);material=int(expected['Material']);row=actual[warehouse][material]
    assert Decimal(row['Nc'])==Decimal(str(expected['Price']))==native[warehouse][material],('Price',warehouse,material,row['Nc'],expected['Price'])
    assert row['NazMat'].strip()==expected['Name'].strip(),('Name',warehouse,material)
    assert row['Jedm'].strip().upper()==expected['Unit'].strip().upper(),('Unit',warehouse,material)
    candidates=[values for values in printed[warehouse] if Decimal(material) in values or str(material) in values]
    assert len(candidates)==1,(warehouse,material,candidates)
    numbers=[value for value in candidates[0] if isinstance(value,Decimal)]
    assert numbers==[Decimal(material),Decimal(str(expected['Price']))],('Printed price / blank count and value columns',warehouse,material,numbers)
    checks+=1
result={'MaterialGroups':checks,'Warehouses':[1000,1001,1002],'Verified':['Original Fox ASCII prices','WPF actual rendered prices','Material names','Units','Blank count and value columns','Same material membership'],'Failures':0}
(work/'fox_inventory_slips_comparison.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print('PASS Fox / WPF inventory slips:',checks,'materials in 3 warehouses; prices, names, units and blank columns match')

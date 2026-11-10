"""Reproduce ULazi.frx bands in RDLC without starting or modifying Fox."""
from pathlib import Path
import sys, json, hashlib
from lxml import etree as E
from InspectFoxCostReports import rows

source, work = map(Path, sys.argv[1:3])
records = list(rows(source / 'ulazi.frx'))
ns = 'http://schemas.microsoft.com/sqlserver/reporting/2008/01/reportdefinition'
rd = 'http://schemas.microsoft.com/SQLServer/reporting/reportdesigner'
root = E.Element('{' + ns + '}Report', nsmap={None: ns, 'rd': rd})
def el(parent, name, text=None, **attrs):
    node = E.SubElement(parent, '{' + ns + '}' + name, **attrs)
    if text is not None: node.text = str(text)
    return node
def inch(value): return f'{value:.6f}in'
width = 7.64
fields = dict.fromkeys(['Id','FoxRecno','Redbr','Ident'], 'System.Int32')
fields.update(dict.fromkeys(['Nazmat','Jedm','Tarifa'], 'System.String'))
fields.update(dict.fromkeys(['Kolic','Fcj','Fvrijed','Troskovi','Cartro','Trospe','Zavtro','TotalCosts','Ppp','Porppp','Nc','Vrijed'], 'System.Decimal'))
params = ['Organization','Number','DocumentNumber','DocumentDate','SupplierCode','SupplierName','Account','AccountName']
mapping = {'date()':'=Today()', 'ident':'=Fields!Ident.Value','analst':'=Parameters!SupplierCode.Value',
 'brdok':'=Parameters!DocumentNumber.Value','datnar':'=Parameters!DocumentDate.Value','kolic':'=Fields!Kolic.Value',
 'brfak':'=Parameters!Number.Value','anazivorg':'=Parameters!Organization.Value','redbr':'=Fields!Redbr.Value',
 'vrijed':'=Fields!Vrijed.Value','mat.nc':'=Fields!Nc.Value','analdatr.ime':'=Parameters!SupplierName.Value',
 'mater.nazmat':'=Fields!Nazmat.Value','mater.jedm':'=Fields!Jedm.Value','mater.siftar':'=Fields!Tarifa.Value',
 'fvrijed':'=Fields!Fvrijed.Value','fcj':'=Fields!Fcj.Value','cartro+zavtro+trospe':'=Fields!TotalCosts.Value',
 'mat.troskovi':'=Fields!Troskovi.Value','mat.konto1':'=Parameters!Account.Value','skontdat.nazkont':'=Parameters!AccountName.Value',
 'mat.ppp':'=Fields!Ppp.Value','cartro':'=Fields!Cartro.Value','zavtro':'=Fields!Zavtro.Value',
 'mat.porppp':'=Fields!Porppp.Value','trospe':'=Fields!Trospe.Value',
 '"strana:"+" "+alltrim(str(_pageno))':'="Strana: " & Globals!PageNumber'}
# The original uses legacy code-page glyphs; retain the actual Bosnian labels.
labels = {8:'Ulaz u magacin - od dobavljača',9:'Šifra /Naziv\nrobe',14:'Magacinski konto zadužuje:',
 17:'Količina',30:'Dobavljač:',44:'Troškovi',46:'Troškovi (analitički):',47:'Carinski troškovi:',
 48:'Ostali troškovi:',59:'Ukupno troškovi:',68:'Špediterski troškovi:'}

def add_objects(parent, lower, upper):
    for r in records:
        if r.get('OBJTYPE') not in ['5','6','8']: continue
        y = float(r['VPOS']) / 10000
        if not lower <= y < upper: continue
        x, h, w = [float(r[k])/10000 for k in ['HPOS','HEIGHT','WIDTH']]
        number = r['Recno']
        if r['OBJTYPE']=='6':
            box=el(parent,'Line',Name=f'Fox{number}')
            el(box,'Top',inch(y-lower));el(box,'Left',inch(x));el(box,'Height','0in');el(box,'Width',inch(w))
            style=el(box,'Style');border=el(style,'Border');el(border,'Style','Solid');el(border,'Width','0.5pt')
            continue
        expr=r['EXPR'].lower().strip()
        value=labels.get(number,r['EXPR'].strip('"').replace('\r','\n').strip()) if r['OBJTYPE']=='5' else mapping[expr]
        if r['TOTALTYPE']=='2':
            field=value[len('=Fields!'):-len('.Value')]
            value=f'=Sum(Fields!{field}.Value)' if lower > 5 else f'=RunningValue(Fields!{field}.Value, Sum, Nothing)'
        box=el(parent,'Textbox',Name=f'Fox{number}')
        el(box,'CanGrow','true');el(box,'KeepTogether','true')
        paragraphs=el(box,'Paragraphs');p=el(paragraphs,'Paragraph');runs=el(p,'TextRuns');run=el(runs,'TextRun');el(run,'Value',value)
        style=el(run,'Style');el(style,'FontFamily','Arial');el(style,'FontSize',('9' if r['OBJTYPE']=='5' and number!=8 else r['FONTSIZE'])+'pt')
        if r['FONTSTYLE']=='1':el(style,'FontWeight','Bold')
        if expr=='date()':el(style,'Format','dd.MM.yyyy')
        elif '999' in r['PICTURE'] or expr=='mat.ppp':
            precision = 9 if expr=='mat.nc' else 4 if expr=='fcj' else 0 if expr=='kolic' else 2
            el(style,'Format','N'+str(precision))
        ps=el(p,'Style')
        if '999' in r['PICTURE'] or expr=='mat.ppp':el(ps,'TextAlign','Right')
        # Widen the title into the free area, preserving its centered appearance.
        if number==8:x,w=2.05,4.45;el(ps,'TextAlign','Center')
        if number in [25,36]:w=.5
        if number==9:w=.8
        if number==33:w=.8
        if number==26:x,w=6.70,.88
        if number==54:w=.55
        if number in [15,50]:w=.46
        if number==30:w=.65
        if number==60:w=.65
        if number==51:x,w=2.02,.65
        if number==53:x=.72+2
        if number==52:x,w=2.83,4.65
        # Small printer metrics differ between GDI and RDLC. Allow long labels enough room.
        if number in [14,46,47,48,59,68]:w=max(w,1.32)
        el(box,'Top',inch(y-lower));el(box,'Left',inch(x));el(box,'Height',inch(max(h,.17)));el(box,'Width',inch(w))
        style=el(box,'Style')
        for side in ['Left','Right','Top','Bottom']:el(style,'Padding'+side,'0pt')

body=el(root,'Body');items=el(body,'ReportItems')
title=el(items,'Rectangle',Name='ReceiptTitle');ti=el(title,'ReportItems');add_objects(ti,0,1.7605)
el(title,'KeepTogether','true');el(title,'Height','1.7605in');el(title,'Width',inch(width));el(title,'Style')
tablix=el(items,'Tablix',Name='ReceiptLines');tb=el(tablix,'TablixBody');cols=el(tb,'TablixColumns');el(el(cols,'TablixColumn'),'Width',inch(width));trs=el(tb,'TablixRows')
for name,lo,height in [('ColumnHeader',1.9605,.8334),('ReceiptDetail',2.9939,2.1042),('ReceiptTotals',5.7251,.6563)]:
    tr=el(trs,'TablixRow');el(tr,'Height',inch(height));cells=el(tr,'TablixCells');cell=el(cells,'TablixCell');contents=el(cell,'CellContents')
    rect=el(contents,'Rectangle',Name=name);ri=el(rect,'ReportItems');add_objects(ri,lo,lo+height)
    el(rect,'KeepTogether','true');el(rect,'Style')
ch=el(tablix,'TablixColumnHierarchy');el(el(ch,'TablixMembers'),'TablixMember')
rh=el(tablix,'TablixRowHierarchy');members=el(rh,'TablixMembers')
member=el(members,'TablixMember');el(member,'KeepWithGroup','After');el(member,'RepeatOnNewPage','true')
member=el(members,'TablixMember');el(member,'Group',Name='ReceiptDetailGroup')
member=el(members,'TablixMember');el(member,'KeepWithGroup','Before')
el(tablix,'DataSetName','DataSet1');el(tablix,'Top','1.7605in');el(tablix,'Height','3.5939in');el(tablix,'Width',inch(width));el(tablix,'Style')
el(body,'Height','5.3544in');el(body,'Style');el(root,'Width',inch(width))
page=el(root,'Page');footer=el(page,'PageFooter');el(footer,'Height','.227in');el(footer,'PrintOnFirstPage','true');el(footer,'PrintOnLastPage','true');fi=el(footer,'ReportItems');add_objects(fi,5.2981,5.5251);el(footer,'Style')
for key,value in [('PageHeight','11.6929in'),('PageWidth','8.2677in'),('LeftMargin','.3in'),('RightMargin','.3in'),('TopMargin','.3in'),('BottomMargin','.3in')]:el(page,key,value)
el(page,'Style');el(root,'Language','de-DE');el(root,'ConsumeContainerWhitespace','true')
ds=el(el(root,'DataSources'),'DataSource',Name='ReceiptData');cp=el(ds,'ConnectionProperties');el(cp,'DataProvider','System.Data.DataSet');el(cp,'ConnectString','/* Local Connection */')
dataset=el(el(root,'DataSets'),'DataSet',Name='DataSet1');query=el(dataset,'Query');el(query,'DataSourceName','ReceiptData');el(query,'CommandText','/* Local Query */');fs=el(dataset,'Fields')
for name,kind in fields.items():
    f=el(fs,'Field',Name=name);el(f,'DataField',name);E.SubElement(f,'{'+rd+'}TypeName').text=kind
rp=el(root,'ReportParameters')
for name in params:
    p=el(rp,'ReportParameter',Name=name);el(p,'DataType','String');el(p,'AllowBlank','true');el(p,'Prompt',name)
target=Path(__file__).resolve().parent.parent/'Materijalno.UI/Izvjestaji/UlazMaterijala.rdlc'
E.ElementTree(root).write(str(target),encoding='utf-8',xml_declaration=True,pretty_print=True)
manifest=[]
for name in ['ulazi.frx','ulazi.frt','ulazi1.scx','ulazi1.sct']:
    p=source/name;manifest.append({'Path':str(p),'Hash':hashlib.sha256(p.read_bytes()).hexdigest()})
(work/'fox_manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf8')
print('Generated ULazi layout: A4 portrait, original bands, positions, fields and totals.')

"""Update the two cost report templates to use the original Fox grouping."""
import copy
from pathlib import Path
import xml.etree.ElementTree as E

NS='http://schemas.microsoft.com/sqlserver/reporting/2008/01/reportdefinition'
RD='http://schemas.microsoft.com/SQLServer/reporting/reportdesigner'
E.register_namespace('',NS); E.register_namespace('rd',RD)
def tag(s):return '{'+NS+'}'+s
def elem(s,text=None):
    x=E.Element(tag(s)); x.text=text; return x
def values(row,texts):
    for cell,text in zip(row.findall('./'+tag('TablixCells')+'/'+tag('TablixCell')),texts):
        vs=cell.findall('.//'+tag('TextRun')+'/'+tag('Value'))
        if vs:vs[0].text=text
def sort(member,expression):
    old=member.find(tag('SortExpressions'))
    if old is not None:member.remove(old)
    sorts=elem('SortExpressions');entry=elem('SortExpression');entry.append(elem('Value',expression));sorts.append(entry)
    group=member.find(tag('Group'));member.insert(1 if group is not None else 0,sorts)
def field(root,name,typename):
    fields=root.find('.//'+tag('DataSet')+'/'+tag('Fields'))
    if any(f.get('Name')==name for f in fields):return
    f=elem('Field');f.set('Name',name);f.append(elem('DataField',name));t=E.SubElement(f,'{'+RD+'}TypeName');t.text=typename;fields.append(f)

for bydoc in [False,True]:
    name='PregledTroskovaPoKontima'+('INalozimaZaSvaSkladista' if bydoc else '')
    path=Path('Materijalno.UI/Izvjestaji')/(name+'.rdlc'); tree=E.parse(path); root=tree.getroot()
    for run in root.iter(tag('TextRun')):
        value=run.find(tag('Value'))
        if value is not None and value.text and ('Fields!Vrijed.Value' in value.text or 'Fields!UkupnoVrijed.Value' in value.text):
            style=run.find(tag('Style'))
            if style is None:style=E.SubElement(run,tag('Style'))
            for key,text in [('Format','n2'),('Language','bs-Latn-BA')]:
                prop=style.find(tag(key))
                if prop is None:prop=E.SubElement(style,tag(key))
                prop.text=text
    field(root,'UkupnoVrijed','System.Decimal');field(root,'DatumDokumenta','System.DateTime')
    field(root,'FoxOrder','System.Int64')
    tablix=root.find('.//'+tag('Tablix')); members=tablix.find('./'+tag('TablixRowHierarchy')+'/'+tag('TablixMembers'))
    group=members[0]
    if not bydoc:
        group.find('.//'+tag('GroupExpression')).text='=Fields!Kljnaz.Value'
        for v in root.iter(tag('Value')):
            if v.text=='=Sum(Fields!Vrijed.Value)':v.text='=Sum(Fields!UkupnoVrijed.Value)'
        details=group.find('./'+tag('TablixMembers'))[1];sort(details,'=Fields!Konto1.Value')
    else:
        # Fox menu kontana1 -> kontona1.frx: account, then all documents.
        if group.find(tag('Group')).get('Name')=='Skladiste':
            rows=tablix.find('./'+tag('TablixBody')+'/'+tag('TablixRows'))
            rows.remove(rows[4]);rows.remove(rows[0])
            account=group.find('./'+tag('TablixMembers'))[1]
            members.remove(group);members.insert(0,account);group=account
        group.find('.//'+tag('GroupExpression')).text='=Fields!Konto1.Value'
        sort(group,'=Fields!Konto1.Value')
        children=group.find('./'+tag('TablixMembers'))
        sort(children[1],'=Fields!FoxOrder.Value')
        repeat=children[0].find(tag('RepeatOnNewPage'))
        if repeat is None:children[0].append(elem('RepeatOnNewPage','true'))
        for value in tablix.iter(tag('Value')):
            if value.text=='Ukupan iznos za konto:':value.text='Ukupno konto:'
        # The document date is in the second body column, underneath the
        # account name, as in kontona1.frx.
        header=root.find('./'+tag('Page')+'/'+tag('PageHeader')+'/'+tag('ReportItems'))
        date_label=next(t for t in header if t.get('Name')=='Textbox34')
        widths=[c.findtext(tag('Width')) for c in tablix.find('./'+tag('TablixBody')+'/'+tag('TablixColumns'))]
        date_label.find(tag('Left')).text=widths[0]
        date_label.find(tag('Width')).text=widths[1]
        date_label.find(tag('Top')).text='2.76774cm'
    E.indent(tree,space='  ');tree.write(path,encoding='utf-8',xml_declaration=True)

"""Small layout repairs identified from real multi-line printed reports."""
import re
from pathlib import Path
root=Path(__file__).resolve().parent.parent/'Materijalno.UI'/'Izvjestaji'
def edit(name, transform):
    file=root/(name+'.rdlc')
    source=file.read_text(encoding='utf-8-sig')
    file.write_text(transform(source),encoding='utf-8-sig')
def body_height(source, value):
    return re.sub(r'(</ReportItems>\s*<Height>)[^<]+(</Height>\s*<Style\s*/>\s*</Body>)',lambda m:m[1]+value+m[2],source)
def issue(source):
    source=source.replace('<Top>6.56593cm</Top>','<Top>4.55cm</Top>')
    return body_height(source,'5.2cm')
edit('IzlazMaterijala',issue)
def finance(source):
    source=source.replace('<Top>3.81709cm</Top>','<Top>2.45cm</Top>').replace('<Top>4.11668cm</Top>','<Top>2.75cm</Top>')
    return body_height(source,'3.4cm')
edit('NaloziZaFinansijePrintanjeNaloga',finance)
def inventory(source):
    rows=re.search(r'<TablixRows>(.*?)</TablixRows>',source,re.S)
    parts=re.findall(r'<TablixRow>.*?</TablixRow>',rows[1],re.S)
    assert len(parts)==2 and not re.search(r'<Value>[^<]+',parts[1]),'Unexpected inventory footer'
    source=source.replace(parts[1],'')
    source=source.replace('<TablixMember>\n              <KeepWithGroup>Before</KeepWithGroup>\n            </TablixMember>','')
    # The removed empty footer is the second static member, with no grouping.
    source=re.sub(r'(<TablixRowHierarchy>.*?</TablixMember>)\s*<TablixMember>\s*<KeepWithGroup>Before</KeepWithGroup>\s*</TablixMember>',r'\1',source,flags=re.S)
    return body_height(source,'1.0cm')
edit('UnosInventurnogStanja',inventory)
def differences(source):
    source=source.replace('<LeftMargin>2cm</LeftMargin>','<LeftMargin>0.5cm</LeftMargin>').replace('<RightMargin>2cm</RightMargin>','<RightMargin>0.5cm</RightMargin>')
    source=source.replace('<Top>2.09903cm</Top>','<Top>1.5cm</Top>').replace('<Top>2.06375cm</Top>','<Top>1.45cm</Top>')
    def value(match):
        text=match[1]
        if 'Fields!InvVrijed.Value' in text and 'Fields!MatVrijed.Value' in text and text.startswith('=IIF('):
            shortage='Fields!InvVrijed.Value &lt; Fields!MatVrijed.Value' in text
            gap='Fields!MatKolic.Value - Fields!InvKolic.Value' if shortage else 'Fields!InvKolic.Value - Fields!MatKolic.Value'
            return '<Value>=System.Math.Round(System.Math.Max('+gap+', 0D) * Fields!Nc.Value, 2, System.MidpointRounding.AwayFromZero)</Value>'
        return match[0]
    source=re.sub(r'<Value>(.*?)</Value>',value,source,flags=re.S)
    return body_height(source,'2.15cm')
edit('StampanjeInventurnihZaliha',differences)
print('Repaired totals spacing, inventory empty footer, and inventory difference width/valuation.')

import xml.etree.ElementTree as ET
from pathlib import Path
ns={'r':'http://schemas.microsoft.com/sqlserver/reporting/2008/01/reportdefinition'}
root=Path(__file__).resolve().parent.parent/'Materijalno.UI'/'Izvjestaji'
for name in ['IzlazMaterijala','NaloziZaFinansijePrintanjeNaloga','UnosInventurnogStanja','StampanjeInventurnihZaliha']:
    xml=ET.parse(root/(name+'.rdlc'));print(name)
    for item in xml.findall('r:Body/r:ReportItems/*',ns):
        print(item.tag.split('}')[-1],item.get('Name'),item.findtext('r:Top','0',ns),item.findtext('r:Height','0',ns),' '.join(item.itertext()).strip()[:38])
        if item.tag.endswith('Tablix'):
            for row in item.findall('r:TablixBody/r:TablixRows/r:TablixRow',ns):
                values=[e.text or '' for e in row.findall('.//r:Value',ns)];print('ROW',row.findtext('r:Height','0',ns),values)
    page=xml.find('r:Page',ns)
    print('PAGE',[(e.tag.split('}')[-1],e.text) for e in page if not list(e)])

import json
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

work = Path(sys.argv[1])
root = Path(__file__).resolve().parent.parent / 'Materijalno.UI' / 'Izvjestaji'
ns = {'r': 'http://schemas.microsoft.com/sqlserver/reporting/2008/01/reportdefinition'}
def cm(value):
    if not value: return 0
    return float(re.sub('[a-z]', '', value)) * (2.54 if value.endswith('in') else 1)
for name in ['Dnevnik', 'AnalitickaKartica', 'IzlazMaterijala', 'ReportPovrat', 'UnosInventurnogStanja']:
    xml = ET.parse(root / (name + '.rdlc'))
    body = xml.find('r:Body', ns)
    extent = max(cm(e.findtext('r:Top', '0cm', ns)) + cm(e.findtext('r:Height', '0cm', ns)) for e in body.find('r:ReportItems', ns))
    page = xml.find('r:Page', ns)
    print(name, 'body', body.findtext('r:Height', '', ns), 'min-extent', round(extent, 4), 'header', page.findtext('r:PageHeader/r:Height', '', ns), 'footer', page.findtext('r:PageFooter/r:Height', '', ns))
    if '--apply' in sys.argv:
        source = (root / (name + '.rdlc')).read_text(encoding='utf-8-sig')
        height = f'{extent + .05:.4f}cm'
        source = re.sub(r'(</ReportItems>\s*<Height>)[^<]+(</Height>\s*<Style\s*/>\s*</Body>)', lambda m: m[1]+height+m[2], source)
        (root / (name + '.rdlc')).write_text(source, encoding='utf-8-sig')

manifest = json.loads((work / 'reports_manifest.json').read_text(encoding='utf-8-sig'))
changed = set()
for report in manifest:
    if report['Format'] != 'PDF': continue
    missing = set()
    for warning in report.get('Warnings', []):
        if warning['Code'] == 'rsMissingFieldInDataSet':
            missing.update(re.findall('Field [‘\']([^’\']+)[’\']', warning['Message']))
    if not missing: continue
    output = report['Report']
    status = output.split('_')[-1]
    if output.startswith('Dokument_'):
        name = 'Medjuskladisnica' if status.startswith('M-') else 'ReportPovrat' if status in ('61-213','61-215','61-216','61-218') else 'IzlazMaterijala'
    elif output.startswith('Finansije_'): name = 'NaloziZaFinansijePrintanjeNaloga'
    elif output.startswith('PocetnoStanje_'): name = 'PocetnoStanjePrviPut'
    elif output == 'AnalitickaKartica_sa_prenosom': name = 'AnalitickaKartica'
    else: name = output
    file = root / (name + '.rdlc')
    source = file.read_text(encoding='utf-8-sig')
    used = set(re.findall(r'Fields!([A-Za-z0-9_]+)\.Value', source))
    assert not missing & used, (name, 'Missing USED fields', missing & used)
    if name not in changed:
        print(name, 'unused missing definitions', sorted(missing))
        if '--apply' in sys.argv:
            for field in missing:
                source = re.sub(r'\s*<Field Name="'+re.escape(field)+r'">.*?</Field>', '', source, flags=re.S)
            file.write_text(source, encoding='utf-8-sig')
        changed.add(name)

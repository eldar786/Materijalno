"""Keep original XML formatting while changing only identifier metadata/limits."""
from pathlib import Path
import re
from lxml import etree as E

root = Path(__file__).resolve().parent.parent
run = Path((root / 'artifacts/current_material_identifiers_run.txt').read_text().strip())
for old in (run / 'source_before/Materijalno.UI').rglob('*'):
    if old.suffix not in ('.xaml', '.rdlc'):
        continue
    dest = root / old.relative_to(run / 'source_before')
    original = old.read_bytes()
    current = dest.read_bytes()
    if current == original:
        continue
    text = original.decode('utf-8-sig')
    if old.suffix == '.rdlc':
        text = re.sub(r'(<Field Name="Ident">.*?<rd:TypeName>).*?(</rd:TypeName>)',
                      r'\g<1>System.String\2', text, flags=re.S)
    else:
        current_tree = E.fromstring(current)
        limited_names = {n.get('{http://schemas.microsoft.com/winfx/2006/xaml}Name')
                         for n in current_tree.iter()
                         if isinstance(n.tag, str) and n.tag.endswith('}TextBox') and n.get('MaxLength') == '9'}
        def limit(match):
            tag = match.group(0)
            name = re.search(r'x:Name="([^"]+)"', tag)
            if (name and name.group(1) in limited_names) or re.search(r'Text="\{Binding (?:[^"}]*\.)?(?:Ident|Sifmat)(?:[},])', tag):
                if 'MaxLength=' not in tag:
                    return re.sub(r'(\s*/?>)$', r' MaxLength="9"\1', tag)
            return tag
        text = re.sub(r'<TextBox\b[^>]*>', limit, text)
    # Serialization whitespace is irrelevant; all structural edits must match.
    def canonical(data):
        parser = E.XMLParser(remove_blank_text=True)
        return E.tostring(E.fromstring(data, parser), method='c14n')
    result = (b'\xef\xbb\xbf' if original.startswith(b'\xef\xbb\xbf') else b'') + text.encode('utf-8')
    assert canonical(result) == canonical(current), str(dest)
    dest.write_bytes(result)
    print(dest.name)

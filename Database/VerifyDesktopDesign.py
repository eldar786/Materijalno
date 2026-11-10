"""Check presentation changes against the original menu and business bindings."""
from pathlib import Path
from lxml import etree as E
from collections import Counter
import json
ROOT=Path(__file__).resolve().parents[1]; RUN=Path((ROOT/'artifacts/current_design_run.txt').read_text().strip())
NS={'p':'http://schemas.microsoft.com/winfx/2006/xaml/presentation','x':'http://schemas.microsoft.com/winfx/2006/xaml'}
P='{'+NS['p']+'}'; X='{'+NS['x']+'}'
def business(tree):
    vals=[]
    names=set(tree.xpath('//@x:Name',namespaces=NS))
    for el in tree.iter():
        if not isinstance(el.tag,str):continue
        for k,v in el.attrib.items():
            # Label.Target is a new accessibility relationship. The old warehouse
            # form also had an error label for textBox1, which does not exist.
            if k=='Target':continue
            if 'Validation.Errors' in v and 'ElementName=textBox1,' in v and 'textBox1' not in names:continue
            if '{Binding' in v and 'TemplatedParent' not in v:vals.append(('binding',v))
            if k in ('Command','Gesture','KeyDown','GotFocus','LostFocus','Click','SelectedDateChanged','Loaded','SelectionChanged'):
                vals.append((k,v))
        if el.tag==P+'Binding':vals.append(('BindingElement',tuple(sorted(el.attrib.items()))))
    return Counter(vals)
checks=[]; failures=[]
for before in (RUN/'before/Materijalno.UI').glob('*.xaml'):
    if before.name in ('App.xaml','GlavniView.xaml'):continue
    after=ROOT/'Materijalno.UI'/before.name
    a,b=business(E.parse(str(before))),business(E.parse(str(after)))
    if a!=b:failures.append({'file':before.name,'removed':list((a-b).elements()),'added':list((b-a).elements())})
    else:checks.append(before.name+' business bindings and events unchanged')
tree=E.parse(str(ROOT/'Materijalno.UI/GlavniView.xaml'))
menu=tree.xpath('.//p:StackPanel[@x:Name="glavniStackPanel"]',namespaces=NS)[0]
manifest=[]
for el in menu.iter():
    if el.tag==P+'Button':manifest.append({'name':el.get(X+'Name'),'content':el.get('Content') or ''.join(el.xpath('.//p:TextBlock/@Text',namespaces=NS)),'command':el.get('Command')})
    elif el.tag==P+'Expander':manifest.append({'header':el.find(P+'Expander.Header').find(P+'TextBlock').get('Text')})
if manifest!=json.loads((RUN/'menu_before.json').read_text(encoding='utf-8')):failures.append({'file':'GlavniView.xaml','error':'menu names, commands or order changed'})
else:checks.append('Every menu label, hierarchy, command and position preserved')
(RUN/'source_checks.json').write_text(json.dumps({'checks':checks,'failures':failures},ensure_ascii=False,indent=2),encoding='utf-8')
for f in failures:print(json.dumps(f,ensure_ascii=False))
print('PASSED',len(checks),'FAILED',len(failures))
raise SystemExit(1 if failures else 0)

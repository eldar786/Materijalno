"""Read Fox form definitions as files; never start Fox or connect to its database."""
from pathlib import Path
import json,hashlib,re,sys
from InspectFoxCostReports import rows
source,out=map(Path,sys.argv[1:3]);out.mkdir(parents=True,exist_ok=True)
manifest=[];methods=[]
for name in ('medjskla','medjskl1'):
    for ext in ('scx','sct'):
        file=source/(name+'.'+ext);data=file.read_bytes()
        manifest.append({'Path':str(file),'SHA256':hashlib.sha256(data).hexdigest()})
    for row in rows(source/(name+'.scx')):
        code=row.get('METHODS','');props=row.get('PROPERTIES','')
        code=re.sub(r'(?i)((?:pwd|password|uid|user id)\s*=)[^;\r\n\"]*',r'\1<redacted>',code)
        methods.append({'Form':name,'Object':row.get('OBJNAME'),'Parent':row.get('PARENT'),'Properties':props,'Methods':code})
    (out/(name+'_methods.txt')).write_text('\n\n'.join(m['Object']+'\n'+m['Properties']+'\n'+m['Methods'] for m in methods if m['Form']==name),encoding='utf-8')
(out/'fox_transfer_methods.json').write_text(json.dumps(methods,ensure_ascii=False,indent=2),encoding='utf-8')
(out/'source_manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
for m in methods:
    if any(s in m['Properties'].lower() for s in ('snimi','nova me','slij','odustani','brisanje','izlaz')):
        print(m['Form'],m['Object'],m['Properties'][:400],m['Methods'][:7000],sep='\n')

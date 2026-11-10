"""Inspect Fox source files as data. Never start Fox or open a database connection."""
import json,re,sys,hashlib,shutil
from pathlib import Path
from InspectFoxCostReports import rows
source=Path(sys.argv[1]);out=Path(sys.argv[2]);out.mkdir(parents=True,exist_ok=True)
manifest=[];result=[]
for stem in ['nalog','nalogi','nalogm','nalogweb','nalogiweb','nalogiwebp','nalogmweb','nalu']:
    path=source/(stem+'.scx')
    for ext in ['scx','sct']:
        file=source/(stem+'.'+ext)
        if file.exists():
            data=file.read_bytes();(out/file.name).write_bytes(data)
            manifest.append({'File':file.name,'SHA256':hashlib.sha256(data).hexdigest()})
    for row in rows(path):
        code=row.get('METHODS','')
        if not code:continue
        code=re.sub(r'(?i)((?:PWD|Password|User Id)\s*=)[^;\r\n]*',r'\1<redacted>',code)
        result.append({'Form':stem,'Object':row.get('OBJNAME'),'Code':code})
    (out/(stem+'_methods.txt')).write_text('\n\n'.join(r['Object']+'\n'+r['Code'] for r in result if r['Form']==stem),encoding='utf-8')
for name in ['MAT.MPR','MATWEB.MPR']:
    data=(source/name).read_bytes();(out/name).write_bytes(data);manifest.append({'File':name,'SHA256':hashlib.sha256(data).hexdigest()})
(out/'source_manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
(out/'form_methods.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print('Read-only source inspection:',len(manifest),'files;',len(result),'methods; Fox was not launched.')

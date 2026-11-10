import json,struct,sys
from pathlib import Path
folder=Path(sys.argv[1]);output=Path(sys.argv[2]);tables=[]
for file in sorted(folder.rglob('*')):
    if file.suffix.lower()!='.dbf':continue
    with file.open('rb') as stream:
        header=stream.read(32)
        if len(header)!=32:raise ValueError(str(file))
        count=struct.unpack_from('<I',header,4)[0];head,size=struct.unpack_from('<HH',header,8)
        fields=[]
        while stream.tell()<head:
            first=stream.read(1)
            if first==b'\r':break
            descriptor=first+stream.read(31)
            if len(descriptor)!=32:raise ValueError(str(file))
            fields.append({'Name':descriptor[:11].split(b'\0')[0].decode('ascii'),'Type':chr(descriptor[11]),'Length':descriptor[16],'Decimals':descriptor[17],'Flags':descriptor[18]})
    tables.append({'File':str(file.relative_to(folder)),'Version':header[0],'CodePage':header[29],'Rows':count,'HeaderBytes':head,'RecordBytes':size,'FileBytes':file.stat().st_size,'Modified':file.stat().st_mtime,'Fields':fields})
output.parent.mkdir(parents=True,exist_ok=True)
output.write_text(json.dumps(tables,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'Tables':len(tables),'Rows':sum(t['Rows'] for t in tables),'Types':sorted({f['Type'] for t in tables for f in t['Fields']}),'Files':[{'File':t['File'],'Rows':t['Rows'],'CodePage':t['CodePage'],'Types':''.join(sorted({f['Type'] for f in t['Fields']}))} for t in tables]},ensure_ascii=False))

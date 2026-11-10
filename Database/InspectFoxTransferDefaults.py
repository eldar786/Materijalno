"""Read DBF bytes and saved form source; no Fox process or database writes."""
from pathlib import Path
from collections import Counter
import struct,json,sys,hashlib
source,out=map(Path,sys.argv[1:3]);out.mkdir(parents=True,exist_ok=True)
path=source/'mat.dbf';raw=path.read_bytes();count=struct.unpack_from('<I',raw,4)[0];header,size=struct.unpack_from('<HH',raw,8)
fields=[];offset=1
for pos in range(32,header,32):
    if raw[pos]==13:break
    name=raw[pos:pos+11].split(b'\0')[0].decode('ascii');kind=chr(raw[pos+11]);length=raw[pos+16]
    fields.append(dict(Name=name,Type=kind,Length=length,Decimals=raw[pos+17],Flags=raw[pos+18],Offset=offset));offset+=length
wanted=['FCJ','TROSKOVI','CARTRO','ZAVTRO','PPP','TARIFA','FVRIJED','PORPPP','TROSPE','OURST','MJTST']
stats={name:Counter() for name in wanted};examples=[]
for ix in range(count):
    row=raw[header+ix*size:header+(ix+1)*size]
    if len(row)!=size or row[:1]==b'*':continue
    values={f['Name']:row[f['Offset']:f['Offset']+f['Length']].decode('cp1250',errors='replace').strip() for f in fields if f['Name'] in wanted+['STATUS','BRFAK','REDBR','MEDUS']}
    if values.get('STATUS')!='M':continue
    for name in wanted:stats[name][values.get(name,'<missing>')]+=1
    if len(examples)<4:examples.append(dict(Record=ix+1,**values))
result={'Source':str(path),'SHA256':hashlib.sha256(raw).hexdigest(),'Fields':[f for f in fields if f['Name'] in wanted],
        'Values':{name:dict(c) for name,c in stats.items()},'Examples':examples}
(out/'dbf_defaults.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
for f in result['Fields']:print(f['Name'],f['Type'],f['Length'],f['Decimals'],'nullable flag=',bool(f['Flags']&2),'values=',dict(stats[f['Name']]))
print('Read-only DBF inspection complete.')

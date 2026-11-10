"""Read original FRX/FRT expressions without opening or modifying the Fox database."""
import json,struct,sys
from pathlib import Path
folder=Path(sys.argv[1]);report=sys.argv[2];out=Path(sys.argv[3])
db=(folder/(report+'.frx')).read_bytes();memo=(folder/(report+'.FRT')).read_bytes()
count=struct.unpack_from('<I',db,4)[0];head,size=struct.unpack_from('<HH',db,8)
block=struct.unpack_from('>H',memo,6)[0];fields=[];offset=1
for i in range(32,head,32):
    if db[i]==13:break
    name=db[i:i+11].split(b'\0')[0].decode('ascii');kind=chr(db[i+11]);length=db[i+16]
    fields.append((name,kind,length,offset));offset+=length
result=[]
for index in range(count):
    row=db[head+index*size:head+(index+1)*size]
    if row[:1]==b'*':continue
    obj={'Recno':index+1}
    for name,kind,length,offset in fields:
        raw=row[offset:offset+length]
        if kind=='M':
            pointer=int.from_bytes(raw,'little')
            if pointer:
                start=pointer*block;length=struct.unpack_from('>I',memo,start+4)[0]
                obj[name]=memo[start+8:start+8+length].decode('cp1250',errors='replace').strip()
            else:obj[name]=''
        else:obj[name]=raw.decode('cp1250',errors='replace').strip()
    result.append(obj)
out.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
for row in result:
    if row.get('OBJTYPE')=='8' or any(x in row.get('EXPR','').lower() for x in ['status','kolic','vrijed','nc']):
        print(json.dumps({k:row.get(k) for k in ['Recno','OBJTYPE','NAME','EXPR','VPOS','HPOS','TOTALTYPE']},ensure_ascii=False))

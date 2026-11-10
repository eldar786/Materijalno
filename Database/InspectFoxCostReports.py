import json,struct,sys
from pathlib import Path

def rows(path):
    db=path.read_bytes(); memo=path.with_suffix('.sct' if path.suffix.lower()=='.scx' else '.frt').read_bytes()
    count=struct.unpack_from('<I',db,4)[0]; head,size=struct.unpack_from('<HH',db,8)
    block=struct.unpack_from('>H',memo,6)[0]; fields=[]; offset=1
    for i in range(32,head,32):
        if db[i]==13:break
        name=db[i:i+11].split(b'\0')[0].decode('ascii');kind=chr(db[i+11]);length=db[i+16]
        fields.append((name,kind,length,offset));offset+=length
    for ix in range(count):
        row=db[head+ix*size:head+(ix+1)*size]
        if row[:1]==b'*':continue
        obj={'Recno':ix+1}
        for name,kind,length,offset in fields:
            raw=row[offset:offset+length]
            if kind=='M':
                pointer=int.from_bytes(raw,'little')*block
                value=memo[pointer+8:pointer+8+struct.unpack_from('>I',memo,pointer+4)[0]].decode('cp1250',errors='replace') if pointer else ''
            else:value=raw.decode('cp1250',errors='replace').strip()
            obj[name]=value
        yield obj

if __name__=='__main__':
    folder=Path(sys.argv[1])
    for stem in ['kontarek','kontrosv','kontanal','kontare1','kontana1','invzal']:
        for row in rows(folder/(stem+'.scx')):
            methods=row.get('METHODS','')
            if any(x in methods.lower() for x in ['report form','select ','sum ','set filter']):
                print(stem,row.get('OBJNAME'),methods)
    for stem in ['kontorek','kontrosv','kontonal']:
        for row in rows(folder/(stem+'.frx')):
            if row.get('EXPR','') or row.get('NAME',''):
                print(json.dumps({'Report':stem,**{k:row.get(k) for k in ['Recno','OBJTYPE','EXPR','NAME','TOTALTYPE','RESETTOTAL','INITVAL']}},ensure_ascii=False))

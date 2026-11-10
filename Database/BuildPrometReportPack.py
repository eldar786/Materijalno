"""Package verified actual WPF/RDLC output with a readable test comparison."""
import json
import sys
from decimal import Decimal
from io import BytesIO
from pathlib import Path
from PIL import Image
from pypdf import PdfReader, PdfWriter
from reportlab.pdfgen import canvas
from reportlab.lib import colors
from reportlab.lib.pagesizes import A4, landscape
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle, PageBreak
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont

work=Path(sys.argv[1]);output=Path(sys.argv[2]);output.parent.mkdir(parents=True,exist_ok=True)
def read(name):return json.loads((work/name).read_text(encoding='utf-8-sig'))
manifest=read('reports_manifest.json');fox=read('fox_comparison.json');rows=read('saved_rows_comparison.json')
assert len(manifest)==54 and not rows['Failures'] and fox['PriceMismatches']==fox['ValueMismatches']==0
assert not any(item.get('Warnings') for item in manifest),'Unresolved report warning'
for file,total in [('save_tests.log',67),('reports_tests.log',79),('regression_tests.log',47)]:
    assert f'ALL PASSED {total}' in (work/file).read_text(encoding='utf-8-sig')
pdfmetrics.registerFont(TTFont('ArialQA','C:/Windows/Fonts/arial.ttf'))
pdfmetrics.registerFont(TTFont('ArialQABold','C:/Windows/Fonts/arialbd.ttf'))
pdfmetrics.registerFontFamily('ArialQA',normal='ArialQA',bold='ArialQABold',italic='ArialQA',boldItalic='ArialQABold')
styles=getSampleStyleSheet()
styles.add(ParagraphStyle(name='QA',fontName='ArialQA',fontSize=10,leading=14,spaceAfter=9))
styles.add(ParagraphStyle(name='QATitle',fontName='ArialQABold',fontSize=22,leading=26,spaceAfter=16,textColor=colors.HexColor('#16324f')))
styles.add(ParagraphStyle(name='QAHeading',fontName='ArialQABold',fontSize=12,leading=16,spaceAfter=8))
def p(text):return Paragraph(text,styles['QA'])
def fmt(value,places=2):return f'{Decimal(str(value)):,.{places}f}'.replace(',','X').replace('.',',').replace('X','.')
intro=BytesIO();story=[]
story+=[Paragraph('Provjera Prometa i izvještaja',styles['QATitle']),p('Testni period: <b>01.11.2026. - 04.11.2026.</b> | Završena provjera: 05.11.2026.'),
 p('Unesena su <b>4 ulaza, 4 izlaza, 4 međuskladišnice i 4 povrata</b>: 16 dokumenata, 19 unesenih stavki i 23 reda prometa zbog dvostranih međuskladišnica. Materijali 990201 i 990202; skladišta 1000, 1001 i 1002.'),
 p('<b>Rezultat poređenja:</b> svih 19 nabavnih cijena i vrijednosti odgovara izvornoj Fox logici. Svih 690 provjerenih polja u 23 reda te svih pet završnih salda količine/vrijednosti slaže se. Prazna numerička DBF polja i SQL NULL/0 tretirani su kao isti podrazumijevani iznos.'),
 p('<b>Način provjere:</b> Visual FoxPro 9 izvršio je neizmijenjene metode obračuna i snimanja iz originalnih SCX formi, na zasebnoj kopiji DBF tabela. WPF test koristi stvarne metode obračuna iz ViewModela i servis snimanja. Izvorne DBF datoteke su ostale neizmijenjene.'),
 p('<b>Ponder:</b> nakon prva dva ulaza za materijal 990201 saldo iznosi 300,75 kom i 4.170,88 KM. Ponder je 4.170,88 / 300,75 = <b>13,868262677 KM</b>. Izlaz 61-211 od 24,75 kom sačuvan je i prikazan kao 343,24 KM. Povrat koristi Fox izbor posljednje cijene prema indeksu broja kalkulacije.'),
 p('<b>Provjere:</b> 67 provjera novih unosa, 79 provjera stvarnih izvještaja, 47 regresijskih provjera i 5 provjera WPF prikaza/dugmadi. Izrađena su 54 izlaza za štampu kroz 20 RDLC predložaka i 6 vrsta zbirnih pregleda. Podaci i završni zbirovi provjereni su, a stranice s novim materijalima vizuelno pregledane.'),
 p('<b>Primijenjene ispravke:</b> Fox brojači; cijena povrata; prijemno skladište na međuskladišnici; redoslijed i predznak finansijskih stavki; prazni stari datumi; zaglavlje odabranog perioda; inventurna razlika iz količine i cijene; prikaz pondera u dnevniku; raspored i prazne stranice štampe.'),
 p('<b>Dopuna provjere:</b> Analitička kartica podržava prazne datume starih dokumenata. Dnevnik prikazuje povrat kao nulu u četiri kolone kretanja; cijena ostaje prikazana prema Foxu. Izvorni Fox FRX izrazi i stvarno izračunati RDLC izlaz slažu se u 23 reda i 115 numeričkih polja.'),
 p('<b>Inventurni listići:</b> stvarni Fox ispis i WPF ispis provjereni su za 96 materijala u skladištima 1000, 1001 i 1002. Šifre, nazivi, jedinice mjere, cijene posljednjih zapisa i prazne kolone za prebrojanu količinu/vrijednost slažu se. Izbor inventurnog popisa provjeren je za svih 13 skladišta i nakon promjene skladišta. Za skladište bez snimljenog popisa aplikacija prikazuje jasnu poruku.'),
 p('<b>Granica ove provjere:</b> finansijski nalozi formirani su lokalno i provjereno su uravnoteženi. Stvarni prijenos u Oracle nije izvršen. Testni dokumenti nalaze se u odvojenoj bazi <b>'+ (work/'database_name.txt').read_text().strip()+'</b>.')]
story.append(PageBreak());story.append(Paragraph('Poređenje obračuna Fox / WPF',styles['QATitle']))
data=[['Dokument','Materijal','Skl.','Količina','Fox NC','WPF NC','Fox vrijed.','WPF vrijed.','Ishod']]
cases=read('scenario_saved.json')
for case,result in zip(cases,fox['Cases']):
    data.append([case['Number'],str(case['Ident']),str(case['Warehouse']),fmt(case['Quantity']),fmt(result['Price'],9),fmt(result['WpfPrice'],9),fmt(result['Value']),fmt(result['WpfValue']),'Slaže se'])
table=Table(data,colWidths=[65,61,38,63,116,116,73,73,60],repeatRows=1)
table.setStyle(TableStyle([('FONTNAME',(0,0),(-1,-1),'ArialQA'),('FONTNAME',(0,0),(-1,0),'ArialQABold'),('FONTSIZE',(0,0),(-1,-1),8),('BACKGROUND',(0,0),(-1,0),colors.HexColor('#e3edf5')),('GRID',(0,0),(-1,-1),.3,colors.HexColor('#c6d3de')),('TOPPADDING',(0,0),(-1,-1),4),('BOTTOMPADDING',(0,0),(-1,-1),4),('ALIGN',(3,1),(-2,-1),'RIGHT')]))
story+=[table,Spacer(1,14),p('Vrijednosti su prikazane kao apsolutni iznosi obračuna; u prometu izlazi i izlazna strana međuskladišnice imaju negativan predznak. Međuskladišnice su dodatno provjerene kao dva suprotna reda jednakih cijena i vrijednosti.')]
def footer(c,doc):
    c.setFont('ArialQA',8);c.setFillColor(colors.HexColor('#607080'));c.drawString(30,17,'Kontrolni dokument - testni unosi');c.drawRightString(landscape(A4)[0]-30,17,str(doc.page))
SimpleDocTemplate(intro,pagesize=landscape(A4),leftMargin=30,rightMargin=30,topMargin=28,bottomMargin=32).build(story,onFirstPage=footer,onLaterPages=footer)
writer=PdfWriter();writer.append(PdfReader(intro),outline_item='Rezultat provjere i poređenje cijena')
index=[]
for report in manifest:
    start=len(writer.pages)
    name=report['Report']
    if report['Format']=='PDF':
        writer.append(work/'reports'/(name+'.pdf'),outline_item=name)
    else:
        temp=BytesIO();c=canvas.Canvas(temp)
        for file in report['Pages']:
            file=work/'reports'/file
            with Image.open(file) as im:w,h=im.size
            size=(w/2,h/2);c.setPageSize(size);c.drawImage(str(file),0,0,width=size[0],height=size[1]);c.showPage()
        c.save();writer.append(PdfReader(temp),outline_item=name)
    index.append({'Report':name,'StartPage':start+1,'Pages':len(writer.pages)-start,'Rows':report['Rows']})
writer.add_metadata({'/Title':'Provjera Prometa i izvještaja - Fox / WPF','/Author':'Materijalno QA','/Subject':'Stvarni testni unosi i izvještaji, 01-04.11.2026.'})
writer.write(output)
reopened=PdfReader(output);assert len(reopened.pages)==len(writer.pages)
(work/'final_report_index.json').write_text(json.dumps({'Pdf':str(output),'Pages':len(reopened.pages),'Reports':index},ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'Output':str(output),'Pages':len(reopened.pages),'Reports':len(index),'Bytes':output.stat().st_size},ensure_ascii=False))

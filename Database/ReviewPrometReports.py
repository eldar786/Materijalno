"""Render actual application reports for visual inspection. No synthetic report data."""
import json
import sys
from pathlib import Path
from PIL import Image, ImageDraw
import pypdfium2 as pdfium
from pypdf import PdfReader

work = Path(sys.argv[1])
reports = work / 'reports'
review = work / 'visual_review'
review.mkdir(exist_ok=True)
images = []
stats = []
files = sorted(reports.glob('*.pdf'))
for file in files:
    pdf = pdfium.PdfDocument(str(file))
    reader = PdfReader(file)
    texts = [p.extract_text() or '' for p in reader.pages]
    selected = {0, len(pdf)-1}
    selected.update(i for i, t in enumerate(texts) if '99020' in t or 'E2E PONDER' in t)
    stats.append({'Report': file.stem, 'Pages': len(pdf), 'ShortPages': [i+1 for i, t in enumerate(texts) if len(t.strip()) < 20], 'NewMaterialPages': [i+1 for i, t in enumerate(texts) if '99020' in t]})
    for i in sorted(selected):
        page = pdf[i]
        im = page.render(scale=1.3).to_pil().convert('RGB')
        target = review / (file.stem + '_' + str(i+1).zfill(3) + '.png')
        im.save(target)
        images.append((file.stem + ' p.' + str(i+1), target))
        page.close()
    pdf.close()
for file in sorted(reports.glob('*.png')):
    images.append((file.stem, file))
for n in range(0, len(images), 12):
    batch = images[n:n+12]
    sheet = Image.new('RGB', (1800, 1360), '#d9dde1')
    draw = ImageDraw.Draw(sheet)
    for i, (name, file) in enumerate(batch):
        x, y = (i % 4)*450, (i // 4)*450
        draw.text((x+8, y+4), name, fill='#111111')
        with Image.open(file) as im:
            im.thumbnail((434, 420))
            sheet.paste(im, (x+8, y+24))
    sheet.save(review / ('sheet_' + str(n//12+1).zfill(2) + '.png'))
(work / 'pdf_render_review.json').write_text(json.dumps(stats, indent=2), encoding='utf-8')
print(json.dumps({'PdfReports': len(files), 'ReviewImages': len(images), 'Sheets': (len(images)+11)//12, 'ShortPages': [s for s in stats if s['ShortPages']]}, ensure_ascii=False))

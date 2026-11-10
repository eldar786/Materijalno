import sys
from pathlib import Path
import pypdfium2 as pdfium
source=Path(sys.argv[1]);destination=Path(sys.argv[2])
pdf=pdfium.PdfDocument(str(source))
pdf[0].render(scale=1.4).to_pil().save(destination)
pdf.close()
print(destination)

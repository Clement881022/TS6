"""Create a stable display weight from the upstream OFL Noto Serif TC font."""
from pathlib import Path
import sys

root = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(root / "build/font-tools"))
from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont

font = TTFont(root / "build/NotoSerifTC-variable.ttf")
instantiateVariableFont(font, {"wght": 700}, inplace=True)
font.save(root / "client/Assets/Resources/Fonts/ChibiDisplay.ttf")

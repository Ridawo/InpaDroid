#!/usr/bin/env python3
"""Heurística para ver la unidad fija que un job de una SGBD (.prg) pone en sus resultados *_EINH.

El código de los jobs de un .prg está ofuscado con XOR 0xF7 (ver EdiabasNet.cs). Tras deshacerlo, las
constantes de texto aparecen en orden: "STAT_X_EINH", "<unidad>", "STAT_X_EINH". Si entre dos apariciones
seguidas del nombre hay un literal corto, se toma como unidad. Si la unidad sale de una tabla en tiempo de
ejecución (p.ej. BETRIEBSWTAB de las DDE) no aparece aquí: mirar esa tabla en el volcado de PrgProbe.

Uso: prg_units.py fichero.prg [fichero.prg ...]
"""
import re
import sys

for path in sys.argv[1:]:
    data = bytes(b ^ 0xF7 for b in open(path, 'rb').read())
    units = {}
    names = set(m.group(1) for m in re.finditer(rb'\x00([A-Z0-9_]+_EINH)\x00', data))
    for name in names:
        pos = [m.start() for m in re.finditer(rb'\x00' + re.escape(name) + rb'\x00', data)]
        for a, b in zip(pos, pos[1:]):
            chunk = data[a + len(name) + 2:b]
            if len(chunk) > 200:
                continue
            lits = re.findall(rb'([\x20-\x7e\xb0\xb5\xc4\xd6\xdc\xe4\xf6\xfc\xdf]{1,30})\x00', chunk)
            if lits:
                units.setdefault(name.decode(), set()).add(lits[-1].decode('latin-1'))
    print(f'## {path}')
    for name in sorted(units):
        print(f'{name} = {" | ".join(sorted(units[name]))}')

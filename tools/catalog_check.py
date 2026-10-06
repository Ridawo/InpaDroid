#!/usr/bin/env python3
"""Comprueba E39Catalog.cs contra los volcados de PrgProbe (datos_bmw/e39/analisis/prg/*.txt).

Para cada valor (job + resultado) y cada acción (job + nº de argumentos) dice en qué variantes del grupo
falta. También revisa que los jobs IDENT / FS_LESEN / FS_LOESCHEN existan en todas las variantes.

Uso: tools/catalog_check.py   (desde la carpeta InpaDroid)
"""
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
CATALOG = ROOT / 'src/InpaDroid/Ui/E39/E39Catalog.cs'
DUMPS = ROOT / 'datos_bmw/e39/analisis/prg'

# Variantes E39 de cada grupo (elegidas a mano a partir de los .grp; ver RESUMEN.md).
VARIANTS = {
    'D_0012': ['DDE30DS0', 'DDE40KW0', 'D40M57A1'],
    'D_0032': ['GS20', 'GS832', 'GS836', 'GS851', 'GS855', 'GS8600', 'GS8602', 'GS8603', 'GS8604'],
    'D_0056': ['ABS5', 'ASC5', 'ASC5D', 'ASC57', 'ASC57R75', 'DSC5', 'DSC57', 'DSC3'],
    'D_00A4': ['MRS3', 'MRS4'],
    'D_0080': ['IKE', 'IKI', 'KOMBI39', 'KOMBI39C'],
    'D_00D0': ['LCM', 'LCM_A', 'LCM_II', 'LCM_III', 'LCM_IV'],
    'D_ZKE_GM': ['ZKE3_GM1', 'ZKE3_GM5'],
    'D_0044': ['EWS', 'EWS3', 'EWS3D'],
    'D_005B': ['IHKA39', 'IHKA39_2', 'IHKA39_3', 'IHKA39_4', 'IHKA39_5', 'IHKR39', 'IHR39'],
}


def parse_dump(name):
    jobs = {}
    cur = None
    for line in (DUMPS / f'{name}.txt').read_text(encoding='utf-8', errors='replace').splitlines():
        if line.startswith('JOB '):
            cur = line[4:].strip()
            jobs[cur] = {'args': [], 'results': set()}
        elif line.startswith('== TABLES'):
            break
        elif cur and line.startswith('  ARG '):
            jobs[cur]['args'].append(line.split()[1])
        elif cur and line.startswith('  RES '):
            jobs[cur]['results'].add(line.split()[1])
    return jobs


def main():
    text = CATALOG.read_text(encoding='utf-8')
    # Cada centralita: "E39Ecu Nombre = new(" o "new E39Ecu(".
    blocks = re.split(r'E39Ecu \w+ = new\(|new E39Ecu\(', text)[1:]
    problems = 0
    for block in blocks:
        title, sgbd = re.match(r'\s*"([^"]*)",\s*"([^"]*)"', block).groups()
        variants = VARIANTS[sgbd]
        dumps = {v: parse_dump(v) for v in variants}
        print(f'\n### {title} ({sgbd}): {", ".join(variants)}')
        for job in ('IDENT', 'FS_LESEN', 'FS_LOESCHEN'):
            missing = [v for v in variants if job not in dumps[v]]
            if missing:
                print(f'  [{job}] falta en {missing}')
        page = ''
        for m in re.finditer(r'new E39Page\("([^"]*)"|new E39Value\("([^"]*)", "([^"]*)"|new E39Action\("([^"]*)", "([^"]*)", "([^"]*)"', block):
            if m.group(1):
                page = m.group(1)
                print(f'  -- {page}')
            elif m.group(2):
                job, res = m.group(2), m.group(3)
                missing = [v for v in variants if job not in dumps[v] or res not in dumps[v][job]['results']]
                ok = [v for v in variants if v not in missing]
                flag = 'OK ' if not missing else ('?? ' if ok else 'XX ')
                if not ok:
                    problems += 1
                print(f'     {flag}{job}.{res}' + (f'  falta en {missing}' if missing else ''))
            else:
                title_a, job, args = m.group(4), m.group(5), m.group(6)
                nargs = len(args.split(';')) if args else 0
                missing = [v for v in variants if job not in dumps[v]]
                few = [v for v in variants if job in dumps[v] and len(dumps[v][job]['args']) < nargs]
                ok = [v for v in variants if v not in missing and v not in few]
                if not ok:
                    problems += 1
                info = (f'  falta en {missing}' if missing else '') + (f'  admite menos args en {few}' if few else '')
                print(f'     ACT {"OK " if not info else "?? "}{job}({args}) "{title_a}"{info}')
    print(f'\nValores/acciones sin ninguna variante válida: {problems}')
    return 1 if problems else 0


if __name__ == '__main__':
    sys.exit(main())

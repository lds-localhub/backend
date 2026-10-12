#!/usr/bin/env python3
"""QG-04: verifica a cobertura de linhas por conjunto de modulos.

Le o Cobertura.xml (ja unido) e calcula a cobertura de linhas de dois
conjuntos, separadamente: os modulos criticos e todos os restantes.
Termina com codigo 1 se algum conjunto ficar abaixo do limiar.
"""
import argparse
import sys
import xml.etree.ElementTree as ET


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("report")
    parser.add_argument("--critical", nargs="+", required=True, help="assemblies criticos")
    parser.add_argument("--critical-min", type=float, required=True)
    parser.add_argument("--other-min", type=float, required=True)
    args = parser.parse_args()

    sets = {"criticos": [0, 0, args.critical_min], "restantes": [0, 0, args.other_min]}

    for package in ET.parse(args.report).getroot().iter("package"):
        name = package.get("name")
        key = "criticos" if name in args.critical else "restantes"
        lines = list(package.iter("line"))
        covered = sum(1 for line in lines if int(line.get("hits", "0")) > 0)
        sets[key][0] += covered
        sets[key][1] += len(lines)
        print(f"  {name}: {covered}/{len(lines)} linhas")

    failed = False
    for key, (covered, total, minimum) in sets.items():
        if total == 0:
            print(f"Modulos {key}: sem linhas medidas (limiar de {minimum:g}% nao aplicavel)")
            continue
        pct = 100 * covered / total
        ok = pct >= minimum
        failed |= not ok
        print(f"Modulos {key}: {pct:.1f}% ({covered}/{total}), minimo {minimum:g}% -> {'OK' if ok else 'FALHOU'}")

    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())

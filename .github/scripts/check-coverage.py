#!/usr/bin/env python3
"""QG-04: verifica a cobertura de linhas dos modulos criticos e dos restantes.

Le o Cobertura.xml (ja unido) e classifica cada classe como critica quando o
nome e igual a um prefixo do ficheiro de modulos criticos ou comeca por
"<prefixo>.". Calcula a cobertura de linhas de cada conjunto separadamente e
termina com codigo 1 se algum ficar abaixo do limiar.

Um prefixo sem classes (modulo ainda nao implementado) so produz um aviso.
"""
import argparse
import sys
import xml.etree.ElementTree as ET


def read_prefixes(path: str) -> list[str]:
    with open(path, encoding="utf-8") as file:
        lines = (line.strip() for line in file)
        return [line for line in lines if line and not line.startswith("#")]


def matching_prefix(class_name: str, prefixes: list[str]) -> str | None:
    for prefix in prefixes:
        if class_name == prefix or class_name.startswith(prefix + "."):
            return prefix
    return None


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("report")
    parser.add_argument("--critical-file", required=True, help="ficheiro com os prefixos criticos")
    parser.add_argument("--critical-min", type=float, required=True)
    parser.add_argument("--other-min", type=float, required=True)
    args = parser.parse_args()

    prefixes = read_prefixes(args.critical_file)
    per_prefix = {prefix: [0, 0] for prefix in prefixes}
    sets = {"criticos": [0, 0, args.critical_min], "restantes": [0, 0, args.other_min]}

    for cls in ET.parse(args.report).getroot().iter("class"):
        # So as linhas da propria classe; as de <methods> repetem as mesmas linhas.
        lines = cls.findall("./lines/line")
        covered = sum(1 for line in lines if int(line.get("hits", "0")) > 0)
        prefix = matching_prefix(cls.get("name", ""), prefixes)
        key = "restantes" if prefix is None else "criticos"
        sets[key][0] += covered
        sets[key][1] += len(lines)
        if prefix is not None:
            per_prefix[prefix][0] += covered
            per_prefix[prefix][1] += len(lines)

    print("Prefixos criticos:")
    for prefix, (covered, total) in per_prefix.items():
        if total == 0:
            print(f"  AVISO {prefix}: nenhuma classe encontrada (modulo por implementar ou nome errado)")
        else:
            print(f"  {prefix}: {100 * covered / total:.1f}% ({covered}/{total})")

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

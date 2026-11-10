"""Compare all original Fox fields, respecting DBF numeric zero / SQL NULL defaults."""
import json
import sys
from decimal import Decimal
from pathlib import Path

work = Path(sys.argv[1])
def read(name):
    return json.loads((work / name).read_text(encoding='utf-8-sig'), parse_float=Decimal)
fox = read('fox_saved_rows.json')
wpf = [{k.lower(): v for k, v in row.items()} for row in read('wpf_saved_rows.json')]
numeric = set('kljnaz kljnaz1 ident kolic nc vrijed redbr konto1 fcj troskovi cartro zavtro ppp tarifa fvrijed porppp kontosklad kontosklad1 trospe ourst mjtst'.split())
fields = [k for k in fox[0] if k != 'FoxRecno']
def normalized(field, value):
    value = str(value).strip() if value is not None else ''
    return Decimal(value or '0') if field in numeric else value
def key(row):
    return tuple(normalized(k, row.get(k)) for k in ('status', 'brfak', 'redbr', 'ident', 'kljnaz'))
by_key = {key(row): row for row in wpf}
assert len(by_key) == len(wpf), 'Duplicate WPF keys'
failures = []
checks = 0
for row in fox:
    match = by_key.get(key(row))
    if match is None:
        failures.append({'key': str(key(row)), 'error': 'Missing WPF row'})
        continue
    for field in fields:
        checks += 1
        if normalized(field, row.get(field)) != normalized(field, match.get(field)):
            failures.append({'key': str(key(row)), 'field': field, 'Fox': str(row.get(field)), 'WPF': str(match.get(field))})
balances = {}
for rows, side in ((fox, 'Fox'), (wpf, 'WPF')):
    for row in rows:
        group = '/'.join(str(normalized(k, row[k])) for k in ('ident', 'kljnaz'))
        pair = balances.setdefault(group, {'Fox': [Decimal(0), Decimal(0)], 'WPF': [Decimal(0), Decimal(0)]})[side]
        pair[0] += normalized('kolic', row.get('kolic'))
        pair[1] += normalized('vrijed', row.get('vrijed'))
for group, values in balances.items():
    if values['Fox'] != values['WPF']:
        failures.append({'balance': group, 'values': str(values)})
result = {'FoxRows': len(fox), 'WpfRows': len(wpf), 'FieldsPerRow': len(fields), 'FieldChecks': checks,
          'BalanceGroups': balances, 'Failures': failures,
          'Normalization': 'DBF numeric blank/zero and SQL NULL/empty are equivalent; strings trimmed; dates yyyyMMdd.'}
(work / 'saved_rows_comparison.json').write_text(json.dumps(result, ensure_ascii=False, indent=2, default=str), encoding='utf-8')
print(json.dumps({k: v for k, v in result.items() if k not in ('BalanceGroups', 'Normalization')}, ensure_ascii=False, default=str))
assert len(fox) == len(wpf) == 23 and not failures, 'Saved row comparison failed'

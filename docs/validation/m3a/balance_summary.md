# M3a balance validation

All values below describe final generated terrain after starting-supply repairs. Bridges are included in river area. Starting counts include only resources with a reachable harvest tile inside R1.

| Seed | Plain | Forest | Mountain | Swamp | River | R1 plain |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 482 | 34.99% | 29.98% | 14.99% | 11.99% | 8.06% | 60.25% |
| 777 | 34.96% | 29.98% | 14.99% | 11.99% | 8.08% | 60.25% |
| 202647 | 34.96% | 29.96% | 14.97% | 11.99% | 8.13% | 60.88% |

Target fractions: 35% / 30% / 15% / 12% / 8%. R1 plain target: at least 60%.

| Seed | Wood | Taro | Negi | Mushroom | Fish | Sansai | Total |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 482 | 197 | 98 | 98 | 49 | 30 | 20 | 492 |
| 777 | 197 | 98 | 98 | 49 | 30 | 20 | 492 |
| 202647 | 197 | 98 | 98 | 49 | 30 | 20 | 492 |

Weights: 40 / 20 / 20 / 10 / 6 / 4. Global budgets prevent biome area from overriding material rarity.

| Seed | R1 wood | R1 taro | R1 negi | R1 mushroom | R1 fish | R1 sansai |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 482 | 8 | 14 | 17 | 3 | 4 | 0 |
| 777 | 7 | 16 | 20 | 5 | 3 | 3 |
| 202647 | 11 | 19 | 19 | 6 | 5 | 0 |

Required R1 pool: wood, taro, negi. Each has at least 6 reachable nodes. Bonus is a distinct reachable material.

## Generated objective projections

Each round starts this validation projection with fresh stock and a base-level team at the base, using the same goal seed derivation as LocalGameServer. Live later rounds also incorporate current stock and upgrades. Quantities remain 40–70% of the constructive team schedule.

| Seed | Round | Required | Bonus |
| --- | ---: | --- | --- |
| 482 | 1 | taro 65, negi 71, wood 65 | fish 15 |
| 482 | 2 | negi 63, wood 112, mushroom 22 | sansai 4 |
| 482 | 3 | fish 15, negi 64, taro 56 | sansai 10 |
| 482 | 4 | negi 44, fish 20, wood 78 | sansai 8 |
| 482 | 5 | negi 64, fish 18, wood 100 | taro 58 |
| 777 | 1 | wood 63, taro 62, negi 82 | sansai 8 |
| 777 | 2 | wood 86, mushroom 22, negi 66 | fish 18 |
| 777 | 3 | taro 54, sansai 25, negi 74 | fish 16 |
| 777 | 4 | fish 21, wood 82, mushroom 15 | negi 79 |
| 777 | 5 | negi 83, fish 14, wood 80 | sansai 19 |
| 202647 | 1 | wood 88, taro 65, negi 53 | fish 19 |
| 202647 | 2 | fish 15, mushroom 24, sansai 7 | negi 44 |
| 202647 | 3 | mushroom 20, sansai 11, taro 55 | negi 66 |
| 202647 | 4 | taro 48, negi 52, mushroom 15 | sansai 11 |
| 202647 | 5 | wood 65, fish 23, taro 49 | sansai 16 |

Reproduce:

```powershell
$env:IMONI_MAP_EXPORT_DIR = Join-Path (Get-Location) 'docs/validation/m3a'
dotnet test tools/CoreTests/Imoni.Core.Tests.csproj --logger "trx;LogFileName=dotnet-test.trx" --results-directory docs/validation/m3a
```

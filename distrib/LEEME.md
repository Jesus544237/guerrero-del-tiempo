# distrib — el juego compilado

Una carpeta por plataforma objetivo, tal como las fija el documento de diseño.

| Carpeta | Plataforma | Qué contiene |
|---|---|---|
| `windows-x64/` | Windows 10 u 11, 64 bits | El juego completo, listo para ejecutar |

## Cómo jugarlo

1. Descarga la carpeta `windows-x64` entera.
2. Ejecuta **`GuerreroDelTiempo.exe`**.

No hay que instalar nada más: el motor va dentro de la compilación. No necesita
Unity, ni tiempo de ejecución aparte, ni conexión a internet.

## Requisitos

- Windows 10 o superior, 64 bits
- 4 GB de memoria RAM
- Tarjeta gráfica compatible con DirectX 11
- 700 MB libres en disco

## Controles

| Tecla | Acción |
|---|---|
| A / D | Caminar |
| Espacio | Saltar. Pulsado dos veces, salto doble |
| J | Ataque con la espada |
| W + J | Ataque hacia arriba |
| K | Golpe cargado |
| Shift | Impulso |
| Q | Detener el tiempo |
| E | Chronobreak |
| Esc | Menú de pausa |

Se pueden consultar dentro del juego, en el menú de pausa.

## Nota técnica

Los archivos de más de 100 MB de esta carpeta viajan por **Git LFS**, porque
GitHub no admite archivos sueltos de ese tamaño. Si al clonar el repositorio
aparecen como archivos de texto de pocos bytes, hace falta ejecutar:

```bash
git lfs install
git lfs pull
```

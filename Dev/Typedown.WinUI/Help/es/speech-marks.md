# Marcas de discurso

Las marcas de discurso son pequeñas notas en el texto de una charla que indican cómo decirla: dónde hacer una pausa, qué decir despacio, qué destacar, cuánto tiempo puede durar cada parte. Son texto simple, así que puedes escribirlas o agregarlas desde el panel Discurso, y puedes mostrar tú mismo el texto marcado a un asistente de IA: Caret nunca envía nada.

## Actívalas

**Ver > Modo discurso** muestra las marcas como pequeñas etiquetas y abre el panel Discurso (también en **Marcas de discurso** en la barra lateral). El panel enumera todas las marcas, el tiempo de cada sección y la lista de señales.

## Las marcas

| Marca | Significado |
|---|---|
| `{pause 3s}` | una pausa de tres segundos (`{beat}` es medio segundo) |
| `{wait 5s: applause}` | tiempo que se deja al público |
| `{slow}...{/slow}`, `{fast}...{/fast}` | más lento o más rápido |
| `{loud}...{/loud}`, `{soft}...{/soft}` | más fuerte o más suave |
| `{emphasis}...{/emphasis}` | destacar esto |
| `{tone: dry humour}...{/tone}` | cualquier tono, con tus propias palabras |
| `{cue: look at the back row}` | algo que hacer o recordar |
| `{wpm 130}` | velocidad al hablar de aquí en adelante, en palabras por minuto |
| `{budget 3m}` | tiempo permitido para esta sección, después de su título |

Las palabras siempre están en inglés, sea cual sea el idioma de la charla, para que un archivo signifique lo mismo para todos. Una marca mal escrita queda como texto simple; el panel las cuenta.

## Agregar marcas

- *Haz clic derecho* en el modo discurso para abrir el *anillo de marcas* con las marcas disponibles. El texto seleccionado se envuelve en un par de marcas.
- `Ctrl+Shift+.` agrega una pausa, `Ctrl+Shift+,` una pausa corta y `Ctrl+Shift+E` un énfasis.
- **Configuración > Marcas de discurso** te permite crear tus propias marcas y recetas (varias marcas con un solo clic).

## Tiempo, teleprompter, ensayo

El panel suma el tiempo (las palabras a tu velocidad, más las pausas) y lo compara con el presupuesto de cada sección. **Ver > Teleprompter** abre una ventana que desplaza el texto a tu ritmo, y un ensayo registra cuánto duró realmente cada parte. **Copiar para IA** copia la charla con una breve explicación de las marcas. El teleprompter tiene su propio tema en el menú Ayuda.

## Puntos de partida

**Biblioteca > Plantillas > Agregar plantillas iniciales** incluye un discurso (un brindis) y una presentación (una charla o propuesta) ya marcados, en todos los idiomas de Caret.
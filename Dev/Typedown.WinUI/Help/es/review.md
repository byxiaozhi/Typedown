# Revisión: comentarios y cambios

La función de revisión funciona como las herramientas de revisión de un procesador de texto, pero todo se escribe en el documento como texto simple, así que un colega, otro editor y un asistente de IA también pueden leerlo. Las marcas usan una convención pública llamada CriticMarkup:

| Lo que ves | Cómo se escribe en el archivo |
|---|---|
| texto agregado en verde | `{++added++}` |
| texto eliminado en rojo | `{--deleted--}` |
| un reemplazo | `{~~old~>new~~}` |
| un comentario amarillo | `{==the text==}{>>@Name 2026-10-07: the note<<}` |

Nada se guarda en otro lugar, y la revisión nunca empieza por sí sola: escribir y borrar no crea marcas.

## Agregar un comentario

Selecciona texto, haz clic derecho y elige **Agregar comentario** (o **Revisión > Agregar comentario**). Escribe la nota y revisa tu nombre (al principio es tu nombre de usuario de Windows). El texto se resalta en amarillo, con la nota al lado. Se escribe en el documento, así que **Deshacer** funciona.

## Comparar con otro archivo

**Revisión > Comparar con otro archivo...** pide una versión anterior del documento (por ejemplo, la copia que enviaste) y quién hizo los cambios. Caret escribe las diferencias entre esa versión y el documento en pantalla como una revisión *en una pestaña nueva*; tu documento no cambia. Úsalo para ver qué cambió un colega en el archivo que te devolvió. Si las pestañas están desactivadas (**Configuración > Pestañas y ventanas**), la revisión ocupa el lugar de tu documento en la ventana, así que guarda tu documento primero.

## Seguir los cambios mientras editas

**Revisión > Seguir los cambios** (también un botón en el panel Revisión) recuerda el documento tal como está ahora. Mientras editas, un momento después de que dejas de escribir, Caret muestra lo que cambiaste: el panel derecho de la vista **Dividido** dibuja el documento con lo agregado en verde y lo eliminado en rojo, cada cambio con tu nombre y el día, y el panel Revisión lista los cambios y los cuenta (también en la barra de estado). No se escribe nada en el archivo. **Dejar de seguir los cambios** olvida la versión de partida y deja el documento como está. La lista sirve también para moverte por el documento: *haz clic en un cambio* y Caret salta a él en el editor y lo resalta en el panel derecho, por largo que sea el documento. **Cambio anterior** y **Siguiente cambio** pasan de uno a otro. La ✓ junto a un cambio lo acepta (pasa a formar parte de la versión de partida y sale de la lista); la ✗ lo rechaza (el texto anterior vuelve al documento, y **Deshacer** funciona). **Deshacer aceptar** devuelve los últimos cambios aceptados. **Aceptar todos los cambios** y **Rechazar todos los cambios** hacen lo mismo con todos los cambios seguidos a la vez. Las diferencias dentro de bloques de código solo se pueden aceptar o rechazar todas juntas.

En la vista Visual los cambios se dibujan en el propio texto: el texto agregado tiene fondo verde y una pequeña marca roja queda donde se eliminó texto. Apunta a un cambio o a una marca y una tarjeta muestra el texto anterior, quién y cuándo, con **Aceptar el cambio** y **Rechazar el cambio**. Al hacer clic en un cambio del panel Revisión, el documento se desplaza hasta él y lo resalta un momento. Un cambio que Caret no encuentra en el texto de la página no se dibuja ahí, pero sigue en la lista y en la vista Dividido.

El seguimiento continúa cuando cierras el archivo y lo vuelves a abrir: Caret guarda la versión de partida, tu nombre y el día en que se vio cada cambio por primera vez en su propia carpeta de datos (nunca en tu archivo, y no para un documento que nunca se ha guardado), y los cambios se listan de nuevo al abrir el archivo. Un cambio conserva el día en que se vio por primera vez; no pasa a ser de hoy cada mañana. Si el archivo se editó en otro programa mientras tanto, esas ediciones también aparecen como cambios, y el panel lo dice. **Dejar de seguir los cambios** borra lo guardado, y también lo hace mover el archivo a la Papelera desde Caret; lo que no se abre durante 90 días se limpia.

**Escribir los cambios en el documento** convierte los cambios seguidos en marcas de revisión en el propio texto: cada cambio pasa a ser `{++agregado++}`, `{--eliminado--}` o `{~~antes~>después~~}` seguido de tu nombre y del día en que se vio por primera vez, las mismas marcas que escribe **Comparar con otro archivo...**. Pregunta antes, es un solo paso de **Deshacer** y el seguimiento se detiene. Desde entonces son marcas como cualquier otra: envía el archivo a un colega y acéptalas o recházalas una por una (clic derecho en un cambio) o todas a la vez (**Aceptar todos los cambios**, **Rechazar todos los cambios**). Las diferencias que no se pudieron marcar quedan como texto normal, y la ventana lo avisa.

## Aceptar y rechazar

- *Haz clic derecho en un cambio* (verde, rojo o un reemplazo): **Aceptar el cambio** conserva lo que dice (lo agregado se queda, lo eliminado se va); **Rechazar el cambio** devuelve el texto anterior.
- *Haz clic derecho en un comentario:* **Eliminar el comentario**.
- **Revisión > Aceptar todos los cambios**, **Rechazar todos los cambios** y **Eliminar todos los comentarios** lo hacen en todo el documento, como un solo paso de Deshacer.

## Ocultar las marcas

**Configuración > Editor > Mostrar marcas de revisión** apaga los colores; las marcas se ven entonces como el texto simple que son.

## Conviene saber

- Las marcas son texto, así que un archivo con una revisión se puede guardar, enviar y abrir en cualquier editor.
- Un archivo devuelto con las marcas de un colega se lee del mismo modo: sus cambios se ven en color y tú los aceptas o los rechazas.
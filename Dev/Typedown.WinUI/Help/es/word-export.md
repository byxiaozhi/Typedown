# Exportar a Word

**Archivo** > **Exportar** > **Documento de Word (.docx)** escribe el documento como un archivo de Word de verdad. Se hace en tu PC: Word no tiene que estar instalado y no se sube nada.

## En qué se convierte cada cosa

- Los títulos son títulos de Word, así que el panel de navegación, una tabla de contenido y un lector de pantalla los encuentran. Se conservan las listas, las tablas (la fila de encabezado se repite en cada página), las citas, el código, los enlaces, la negrita, la cursiva y lo demás.
- Las imágenes se ponen en el archivo, con el tamaño que tienen en pantalla y su texto alternativo. Se leen de tu PC; una imagen de la web no se descarga, y una imagen que no se pudo agregar se indica en un mensaje, y su descripción queda en el texto.
- Las notas al pie son notas al pie de Word. Las fórmulas (`$x^2$`) son ecuaciones de Word. Los diagramas (mermaid, flowchart, sequence, vega-lite) son imágenes; PlantUML necesita un servidor, así que sigue siendo código.
- El encabezado del archivo (el «front matter») da el título, el autor, el asunto, la descripción y las palabras clave del archivo de Word, y no se imprime.
- Una línea con `[TOC]` se convierte en una tabla de contenido. Un enlace a un título, como `[ver más abajo](#presupuesto)`, lleva a ese título. Word completa los números de página al abrir el archivo.

## Revisión y discurso

Los comentarios y cambios escritos como marcas (mira **Revisión: comentarios y cambios**) se convierten en comentarios y cambios con seguimiento de Word, con sus autores y sus días, y un colega puede aceptarlos o rechazarlos en Word.

Cuando sigues los cambios del documento, Caret pregunta si hay que exportarlos: **Con los cambios seguidos** muestra cada cambio desde que empezó el seguimiento, y **Solo el texto actual** exporta el texto como está. En ambos casos no se escribe nada en tu documento.

Las marcas de discurso (mira **Marcas de discurso**) son pequeñas notas grises en el archivo.

## Opciones

Las opciones se abren primero y se guardan para la próxima vez.

- **Aspecto**: Sencillo, Informe, Carta o Moderno. Cambian las fuentes, los tamaños, los colores y los espacios; la estructura es la misma.
- **Tamaño de página**, **Orientación** y **Márgenes**.
- **Texto del encabezado** y **Texto del pie de página**: `{title}` se reemplaza por el título del documento y `{date}` por la fecha de hoy. Los **Números de página** van a la derecha del pie de página.
- **Tabla de contenido al principio**.
- **Plantilla**: elige un archivo o una plantilla de Word (`.docx`, `.dotx`) tuya con **Elegir una plantilla...**, y la exportación se escribe sobre ella. Se usan sus estilos, su página, su encabezado y su pie de página, y se descarta su propio texto; las opciones de aspecto y de página no se aplican. **Quitar** vuelve a los aspectos.

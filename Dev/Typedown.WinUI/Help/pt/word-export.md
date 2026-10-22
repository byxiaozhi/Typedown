# Exportar para o Word

**Arquivo** > **Exportar** > **Documento do Word (.docx)** escreve o documento como um arquivo do Word de verdade. Ele é feito no seu PC: o Word não precisa estar instalado e nada é enviado.

## No que cada coisa se transforma

- Os títulos são títulos do Word, então o painel de navegação, um sumário e um leitor de tela os encontram. Listas, tabelas (a linha de cabeçalho se repete em cada página), citações, código, links, negrito, itálico e o resto são mantidos.
- As imagens são colocadas no arquivo, no tamanho que têm na tela e com o texto alternativo. Elas são lidas do seu PC; uma imagem da web não é baixada, e uma imagem que não pôde ser adicionada é listada em uma mensagem, com a descrição dela deixada no texto.
- As notas de rodapé são notas de rodapé do Word. As fórmulas (`$x^2$`) são equações do Word. Os diagramas (mermaid, flowchart, sequence, vega-lite) são imagens; o PlantUML precisa de um servidor, então continua como código.
- O cabeçalho do arquivo (o «front matter») dá o título, o autor, o assunto, a descrição e as palavras-chave do arquivo do Word, e não é impresso.
- Uma linha com `[TOC]` vira um sumário. Um link para um título, como `[veja abaixo](#orcamento)`, leva a esse título. O Word completa os números de página ao abrir o arquivo.

## Revisão e fala

Os comentários e as alterações escritos como marcas (veja **Revisão: comentários e alterações**) viram comentários e alterações controladas de verdade do Word, com seus autores e dias, e um colega pode aceitá-los ou rejeitá-los no Word.

Quando você está controlando as alterações do documento, o Caret pergunta se deve exportá-las: **Com as alterações controladas** mostra cada alteração desde o início do controle, e **Só o texto atual** exporta o texto como está. Nos dois casos nada é escrito no seu documento.

As marcas de fala (veja **Marcas de fala**) são pequenas notas cinzas no arquivo.

## Opções

As opções abrem primeiro e ficam guardadas para a próxima vez.

- **Aparência**: Simples, Relatório, Carta ou Moderno. Elas mudam as fontes, os tamanhos, as cores e os espaçamentos; a estrutura é a mesma.
- **Tamanho da página**, **Orientação** e **Margens**.
- **Texto do cabeçalho** e **Texto do rodapé**: `{title}` é trocado pelo título do documento e `{date}` pela data de hoje. Os **Números de página** ficam à direita do rodapé.
- **Sumário no início**.
- **Modelo**: escolha um arquivo ou modelo do Word (`.docx`, `.dotx`) seu com **Escolher um modelo...**, e a exportação é escrita sobre ele. Seus estilos, sua página, seu cabeçalho e seu rodapé são usados, e o texto dele é descartado; as opções de aparência e de página não valem. **Remover** volta às aparências.

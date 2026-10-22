# Converter documentos e e-mails

O Caret transforma documentos em Markdown: um texto limpo que mantém títulos, listas, tabelas, links e notas de rodapé, e deixa de fora fontes e layout. O resultado costuma ser uma pequena parte do tamanho original, fácil de ler para as pessoas e de absorver para assistentes de IA com muito menos tokens. Tudo é convertido no seu PC: nada é enviado.

## Documentos

Abra **Converter para Markdown** na barra lateral e solte arquivos ou uma pasta inteira, ou use **Escolher arquivos...**. Word, Excel, PowerPoint, PDF e CSV são aceitos. Cada resultado mostra o tamanho antes e depois e uma estimativa de seus tokens de IA. **Copiar tudo para a IA** coloca tudo na área de transferência como um único texto.

Você também pode clicar com o botão direito em um arquivo, em vários arquivos ou em uma pasta no *Explorador de Arquivos* e escolher **Converter para Markdown** (indisponível quando a sua organização desativa isso).

Os arquivos Markdown são salvos ao lado dos originais, ou em uma pasta que você escolher. Um arquivo que já existe nunca é sobrescrito.

## E-mails do Outlook

Abra **E-mails do Outlook** e escolha e-mails salvos do Outlook (`.msg`) ou de outros programas de e-mail (`.eml`). O Caret transforma a conversa inteira em um único arquivo: cada resposta como uma mensagem própria, da mais antiga para a mais recente, sem assinaturas, avisos legais e faixas de "remetente externo", com os anexos convertidos no lugar.

**Ocultar dados pessoais** (ativado por padrão) substitui nomes, endereços de e-mail, números de telefone, números de contas bancárias e números de documentos por marcadores como `[PERSON-1]`, o mesmo marcador para a mesma pessoa, de modo que a conversa continue legível quando você a colar em um assistente de IA. Isso funciona com regras fixas e dígitos verificadores, não com IA. As pessoas são encontradas pelos remetentes e destinatários do e-mail e pelas saudações e despedidas, então um nome que só aparece no meio de uma frase não é encontrado. Confira o resultado antes de compartilhar.

E-mails em inglês, francês, espanhol, polonês e português são compreendidos, e as regras são arquivos simples que uma empresa pode ampliar.

## Solte arquivos em qualquer lugar

Não é preciso abrir antes a página de conversão: arraste arquivos do Word, PDF, Excel, PowerPoint ou e-mails (ou uma pasta inteira) para a janela do Caret, em qualquer lugar, e solte-os quando aparecer **Converter para Markdown**. Uma bandeja pequena, no canto inferior direito, lista os arquivos, cada um com uma barra de progresso e, quando o ponteiro está sobre ele ou o teclado está nele, **Abrir**, **Mostrar no Explorador de Arquivos** (o botão de pasta) e **Comparar**. Um arquivo que falha diz o motivo, com **Tentar novamente**; **Limpar lista** esvazia a bandeja e Esc a fecha.

**Comparar** mostra o original como texto simples à esquerda e o Markdown à direita, e rola os dois juntos. Acima ficam as opções usadas: se as imagens foram extraídas e, em um e-mail, se os dados pessoais foram ocultados e quantos nomes, endereços, números e identificações foram encontrados (no Markdown aparecem em negrito e sublinhados). Esc fecha. Use para ver o que a conversão manteve, descartou ou ocultou antes de compartilhar o resultado.

## Quando algo não é convertido

Um arquivo protegido por senha ou danificado é listado com o motivo. Um PDF digitalizado sem texto não pode ser lido: o Caret não faz reconhecimento de texto.
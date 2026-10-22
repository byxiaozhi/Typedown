<p align="center">
  <img alt="Caret" src="./logo.png" width="96" />
</p>

<h1 align="center">Caret</h1>

<p align="center">
  <strong>Escreva. Controle as alterações. Fale.</strong><br />Um editor de Markdown para Windows com controle de alterações e teleprompter, que transforma documentos do Office em Markdown pronto para a IA e o Markdown em arquivos do Word de verdade.
</p>

<p align="center">
  <a href="README.md">English</a> · <a href="README.fr.md">Français</a> · <a href="README.es.md">Español</a> · <a href="README.pl.md">Polski</a> · Português
</p>

<p align="center">
  <img alt="Controle de alterações no Caret: adições em verde, exclusões em vermelho e a lista de alterações" src="docs/store/screenshots/pt/1-track-changes.png" width="880" />
</p>

---

## Controle de alterações, embutido

Ative **Revisão > Controlar alterações** e o Caret guarda a versão de onde você partiu. Enquanto você edita, cada adição e cada exclusão é desenhada em cor: em verde o que foi adicionado e em vermelho o que foi removido, no modo Visual e ao lado do código no modo Dividido.

- **Uma lista das alterações** para você se mover pelo documento: clique em uma para ir até ela e aceite ou rejeite, ou aceite ou rejeite todas de uma vez
- **Cada alteração lembra o dia em que foi vista pela primeira vez**, e o controle continua depois que você fecha o Caret (o Caret guarda a versão inicial na sua própria pasta de dados, nunca no seu arquivo)
- **Escreva as alterações no documento** com **Revisão > Escrever as alterações no documento**: o autor e a data ficam como texto simples no arquivo, e um colega ou um assistente de IA consegue lê-los sem o Caret
- **Comentários e comparação**: adicione um comentário ao texto selecionado ou compare um arquivo com uma versão anterior para ver o que um colega mudou
- Sem plug-ins, sem servidor, sem conta. As marcas são [CriticMarkup](https://criticmarkup.com), uma convenção aberta de texto simples que outras ferramentas também entendem

## Marcas de fala e teleprompter

<p align="center">
  <img alt="O teleprompter do Caret: texto que rola no seu ritmo, uma faixa de foco e o relógio de apresentação" src="docs/store/screenshots/pt/2-teleprompter.png" width="880" />
</p>

Para quem faz apresentações. Escreva a apresentação no Caret e marque como fazê-la, direto no texto:

- **Marcas de fala**: pausas, ritmo mais lento ou mais rápido, ênfase, trechos fortes e suaves, tom e indicações, escritas como pequenas marcas de texto simples. Crie suas próprias marcas e receitas de um clique em **Configurações > Marcas de fala**
- **Tempo**: o Caret soma o tempo da apresentação (suas palavras no seu ritmo, mais as pausas) e compara com os minutos que você tem para cada seção, com um semáforo
- **Teleprompter** (**Exibir > Teleprompter**): abre em uma segunda tela, se houver. O texto rola com suavidade no seu ritmo, de 25% a 300%, com contagem regressiva, faixa de foco, contagem regressiva até a próxima pausa, uma lista de seções para saltar, e à sua escolha largura do texto, espaçamento entre linhas e conjunto de cores (inclusive de alto contraste), além de espelho e inversão para o vidro de um teleprompter. Pode seguir o plano ou rolar em velocidade constante
- **Relógio de apresentação** (**Exibir > Relógio de apresentação**, ou dentro do teleprompter): o tempo falado, o tempo restante, adiantado ou atrasado em relação ao plano e a hora em que você termina
- **Ensaiar**: leia a apresentação em voz alta e aperte algumas teclas, e o Caret mede quanto tempo cada parágrafo realmente levou e o seu ritmo real. Nenhum áudio é gravado ou reconhecido; o resumo é salvo em um arquivo `.rehearsal.md` ao lado da apresentação (ou copiado para a área de transferência se a apresentação ainda não tem arquivo)
- **Modelos iniciais** de discurso e de apresentação, em todos os idiomas do Caret

| Tecla no teleprompter | O que faz |
| --- | --- |
| `Espaço` | Iniciar ou parar (antes há uma contagem regressiva) |
| `←` `→` | Parágrafo anterior ou seguinte (um controle de apresentação também funciona) |
| `↑` `↓` | Velocidade |
| Roda do mouse, clique | Move a linha de leitura, ou vai até a linha em que você clica |
| `J`, `O` | Lista de seções, opções |
| `M`, `V`, `F` | Espelho, inverter, tela cheia |
| `E` | Ensaiar |

Mais em **Ajuda > O teleprompter** e **Ajuda > Marcas de fala**.

## Exportar para o Word

<p align="center">
  <img alt="As opções da exportação para o Word no Caret: aparência, página, cabeçalho e rodapé, e um modelo" src="docs/store/screenshots/pt/10-word.png" width="880" />
</p>

**Arquivo > Exportar > Documento do Word (.docx)** escreve o documento como um arquivo do Word de verdade, no seu PC: o Word não precisa estar instalado e nada é enviado.

- **Estrutura real do Word**: títulos do Word (assim o painel de navegação funciona), listas, tabelas com a linha de cabeçalho repetida, citações, código, links e imagens com o texto alternativo
- **Notas de rodapé, equações e diagramas**: as notas são notas de rodapé do Word, as fórmulas `$...$` são equações do Word, e os diagramas mermaid, flowchart, sequence e vega-lite são imagens
- **Sua revisão vai junto**: os comentários e as alterações viram comentários e alterações controladas do Word, com seus autores e dias. Um documento cujas alterações você está controlando pode ser exportado com elas, sem escrevê-las no arquivo
- **Sumário e links dentro do documento**: uma linha `[TOC]` e links `[texto](#título)` funcionam no Word
- **Aparências e seu próprio modelo**: quatro aparências (Simples, Relatório, Carta, Moderno), A4, Letter ou Legal, orientação, margens, cabeçalho, rodapé e números de página, ou escrever sobre um modelo do Word seu para usar as fontes, a página, o cabeçalho e o rodapé da sua empresa
- No sentido contrário, **Converter em Markdown** lê um arquivo do Word e o transforma em Markdown

Mais em **Ajuda > Exportar para o Word**.

## Ache seu caminho

- **Paleta de comandos** (`Ctrl+Shift+P`, **Exibir > Paleta de comandos**): digite algumas letras do que você quer fazer, um comando, uma configuração ou uma nota, e pressione Enter
- **Painel de inspeção** (`Ctrl+Shift+I`): uma coluna ao lado da nota com seu esboço, suas marcas de revisão, o tempo da apresentação, todas as ocorrências da busca e o **Histórico** da nota: as versões salvas anteriores, que você pode comparar com o texto atual e restaurar. As versões ficam no seu PC (50 por nota, 30 dias; a cópia mais recente de cada nota sempre fica) e o histórico pode ser desativado na aba Histórico
- **Mapa do documento**: uma faixa fina na borda direita que mostra onde você está na nota e onde estão os títulos, as ocorrências da busca, os comentários e as alterações
- **Uma barra pequena sobre as palavras selecionadas** com os formatos mais usados e **Copiar como texto do WhatsApp**, e uma **barra de status** que diz o que importa (salvo, palavras, tempo de leitura, contagens de revisão) e nada mais
- **Solte arquivos em qualquer lugar da janela** (Word, PDF, Excel, PowerPoint, CSV, e-mails ou uma pasta) para convertê-los em Markdown. Uma bandeja mostra os trabalhos, com Abrir, Mostrar na pasta, **Comparar** (origem e Markdown lado a lado) e Tentar novamente
- **Início**, a um clique com o botão da casa à esquerda das guias (`Ctrl+Shift+H`): a nota para continuar, uma caixa para uma nota rápida, os comentários esperando nas suas notas, uma dica do dia, seus arquivos recentes e suas pastas. Nada é fechado quando o Início aparece
- **Teclado primeiro**: pressione `Alt` para ver qual tecla leva a cada botão, e `Ctrl+Shift+/` para a lista de todos os atalhos
- **Cartões de aparência** em Configurações, com uma prévia ao vivo do esquema de cores e um jeito de voltar a ele com um clique
- **Novidades**: Ajuda > Novidades lista o que mudou em cada versão

## Por que o Caret

Boa parte do que sabemos está em documentos do Word, pastas de trabalho do Excel, apresentações do PowerPoint e PDFs. Assistentes de IA como o Copilot e o ChatGPT funcionam melhor com texto simples, e cada arquivo anexado custa tokens, tempo e limites de envio.

**O Caret converte esses arquivos em Markdown**: um texto limpo que mantém os títulos, listas, tabelas, links e notas de rodapé e deixa de fora todo o resto. O resultado costuma ser **de 90 a 99% menor** que o arquivo original, é lido tão bem por pessoas quanto pela IA e nunca sai do seu PC.

Depois, o Caret oferece um editor de Markdown nativo e tranquilo para ler, editar e organizar o resultado.

| Exemplo (testes do próprio Caret) | Original | Markdown | ≈ Tokens |
| --- | --- | --- | --- |
| Relatório trimestral, Word | 77 KB | 7,3 KB | 1.859 |
| O mesmo relatório em PDF | 276 KB | 7,2 KB | 1.837 |
| Pasta de vendas, Excel | 9,3 KB | 0,2 KB | 55 |
| Apresentação, PowerPoint | 41 KB | 0,2 KB | 47 |

O número de tokens é uma estimativa (cerca de quatro caracteres por token); o valor exato depende do modelo de IA.

## Converter em Markdown

<p align="center">
  <img alt="Caret convertendo arquivos do Word, Excel, PowerPoint e PDF em Markdown" src="docs/store/screenshots/pt/4-convert.png" width="880" />
</p>

Abra **Converter em Markdown** na barra lateral, logo abaixo de Início, e solte arquivos ou uma pasta inteira. Ou, sem abrir a janela do Caret: clique com o botão direito em um arquivo, em vários arquivos ou em uma pasta no Explorador de Arquivos e escolha **Converter em Markdown** (na versão da Store e na versão instalada; um administrador pode desativar, veja o [guia de implantação](docs/deployment.md), em inglês).

- **Word** (.docx): títulos, listas aninhadas, negrito e itálico, links, tabelas (inclusive com células mescladas), notas de rodapé e imagens
- **Excel** (.xlsx): cada planilha visível como tabela, com datas, porcentagens e resultados de fórmulas legíveis
- **PowerPoint** (.pptx): uma seção por slide, com níveis de marcadores, tabelas e anotações do apresentador
- **PDF**: títulos, listas, código, tabelas (com ou sem linhas, mantendo os títulos que abrangem várias colunas) e colunas reconstruídos e lidos na ordem certa, links da web mantidos, sem cabeçalhos, números de página nem carimbos nas margens
- **CSV**: vírgula ou ponto e vírgula, detectados automaticamente
- **E-mails do Outlook** (.msg, .eml): a conversa inteira em um único arquivo, cada resposta como uma mensagem própria, da mais antiga para a mais recente, sem assinaturas, avisos legais nem banners de "remetente externo", com os anexos convertidos no mesmo lugar. Eles têm acesso próprio: **E-mails do Outlook** na barra lateral e em Início, e um cartão com **Escolher e-mails...** na página de conversão. Os e-mails salvos pelo Outlook (arrastados do Outlook para uma pasta) também podem ser soltos como qualquer arquivo

**Ocultar dados pessoais** (ativado por padrão, no cartão de e-mails) substitui nomes, endereços de e-mail, telefones, IBANs, números de cartão e de documentos de identidade (inclusive o CPF e o CNPJ brasileiros) por marcadores como `[PERSON-1]`, sempre o mesmo para a mesma pessoa, para que a conversa continue legível quando você colar em um assistente de IA. Funciona com regras e dígitos verificadores, não com IA: as pessoas são reconhecidas pelos remetentes e destinatários do e-mail e pelos nomes em saudações ("Olá, Daniel,") e despedidas, então um nome que aparece apenas no meio de uma frase não é encontrado. Entende e-mails em português, inglês, francês, espanhol e polonês; as regras são [arquivos JSON](plugins/markitdown-email/src/markitdown_caret_email/rules) que uma empresa pode ampliar.

Cada arquivo mostra o tamanho antes e depois e uma estimativa de seus tokens. **Copiar tudo para a IA** coloca tudo na Área de Transferência como um único texto, pronto para colar em um assistente. Os arquivos Markdown são salvos ao lado dos originais ou em uma pasta que você escolher, e nenhum arquivo existente é sobrescrito.

**Tudo já vem incluído**: sem Python, sem suplementos, sem conexão com a internet e sem enviar nada. Por isso o Caret também serve para notebooks de empresa em que instalar ferramentas é restrito.

## Um editor de Markdown tranquilo

<p align="center">
  <img alt="Um relatório convertido aberto no Caret, em guias ao lado de outros documentos" src="docs/store/screenshots/pt/6-tabs.png" width="880" />
</p>

- **Visual, Código ou Dividido**: edição com formatação, Markdown simples, ou os dois lado a lado com visualização ao vivo
- **Barra de ferramentas de formatação**, menus Parágrafo e Formatar e atalhos conhecidos
- **Tabelas, fórmulas, notas de rodapé e diagramas** (Mermaid, fluxogramas, diagramas de sequência, PlantUML, Vega-Lite)
- **Cole imagens e capturas de tela** direto em uma nota
- **Guias**: vários documentos em uma mesma janela, cada um com seu próprio histórico de desfazer. `Ctrl+Tab` alterna entre eles e `Ctrl+W` fecha o documento, deixando a janela aberta em uma página inicial. Seus documentos salvos são reabertos quando o Caret inicia (dá para desativar em Configurações)
- **Espaço de trabalho por pasta**, **Ir para o arquivo** (`Ctrl+K`), favoritos, arquivos recentes, modelos e Lixeira
- **Modelos iniciais** em todos os idiomas do Caret
- **Salvamento automático** e **recuperação após falha**, até para notas sem título
- **Verificação ortográfica** com sublinhado ondulado vermelho e sugestões no clique com o botão direito, usando o corretor do Windows (offline), nos idiomas que você tem no Windows
- **Revisão em cores**: comentários em amarelo, adições em verde, exclusões em vermelho; aceite ou rejeite cada alteração ou todas de uma vez e compare um arquivo com uma versão anterior para ver o que um colega mudou. Tudo é texto simples dentro do documento ([CriticMarkup](https://criticmarkup.com)), que pessoas e assistentes de IA conseguem ler. Sem servidor e sem conta
- **Copiar como texto do WhatsApp** (menu Editar): coloca a seleção, ou a nota inteira, na Área de Transferência com a formatação do WhatsApp, para colar direto em uma conversa
- **Português (Brasil), inglês, francês, espanhol e polonês**, temas claro e escuro, exportação para HTML, PDF ou texto simples

## Obtenha o Caret

- **Microsoft Store** (recomendado): [obtenha o Caret na Microsoft Store](https://apps.microsoft.com/detail/9n617shlqm8g) ou execute `winget install --source msstore --id 9N617SHLQM8G`. Não há certificado para confiar e ele se atualiza sozinho.
- **GitHub**: baixe o `.msix` e o `Caret.cer` mais recentes nas [versões publicadas](https://github.com/fegyenc/Caret/releases/latest) e siga os dois passos de instalação do [README em inglês](README.md#get-caret).
- **Para grandes empresas, PMEs e autônomos**: o [docs/deployment.md](docs/deployment.md) (em inglês) explica a implantação com o Intune e o Portal da Empresa, o uso da rede e as políticas que desativam a busca de atualizações ou definem o layout e as cores padrão para todos.

Requer o Windows 10 versão 1809 ou posterior (x64 ou ARM64); recomendamos o Windows 11.

## Privacidade

O Caret não coleta nada: sem conta e sem telemetria. Os documentos são convertidos e editados no seu PC. A única conexão que o Caret faz por conta própria é uma busca de atualizações, cerca de uma vez por dia, nas versões que não são instaladas pela Microsoft Store (a do GitHub ou um pacote que a sua organização distribui), e você pode desativá-la; a versão da Store nunca faz isso. Mais detalhes: [PRIVACY.md](PRIVACY.md) (em inglês, francês, espanhol, polonês e português).

## Licença e contribuições

[Licença MIT](LICENSE). O Caret deriva do [Typedown](https://github.com/byxiaozhi/Typedown), de ZZF. Os componentes de terceiros estão listados em [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). Sugestões, ideias e revisões de tradução são bem-vindas pelas [issues do GitHub](https://github.com/fegyenc/Caret/issues); veja também [docs/localization.md](docs/localization.md).
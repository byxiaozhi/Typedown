# Marcas de fala

As marcas de fala são pequenas notas no texto de uma apresentação oral que dizem como entregá-la: onde fazer pausas, o que dizer devagar, o que enfatizar, quanto tempo cada parte pode durar. São texto simples, então você pode digitá-las ou adicioná-las pelo painel Fala, e pode mostrar o texto marcado a um assistente de IA por conta própria: o Caret nunca envia nada.

## Ativar

**Exibir > Modo de apresentação** mostra as marcas como pequenos chips e abre o painel Fala (também em **Marcas de fala** na barra lateral). O painel lista cada marca, o tempo de cada seção e a lista de sinais.

## As marcas

| Marca | Significado |
|---|---|
| `{pause 3s}` | uma pausa de três segundos (`{beat}` é meio segundo) |
| `{wait 5s: applause}` | tempo reservado para o público |
| `{slow}...{/slow}`, `{fast}...{/fast}` | mais devagar ou mais rápido |
| `{loud}...{/loud}`, `{soft}...{/soft}` | mais alto ou mais baixo |
| `{emphasis}...{/emphasis}` | enfatize isto |
| `{tone: dry humour}...{/tone}` | qualquer tom, com suas próprias palavras |
| `{cue: look at the back row}` | algo para fazer ou lembrar |
| `{wpm 130}` | velocidade de fala daqui em diante, em palavras por minuto |
| `{budget 3m}` | tempo permitido para esta seção, colocado depois do título |

As palavras são sempre em inglês, qualquer que seja o idioma da apresentação, para que um arquivo signifique o mesmo para todos. Uma marca escrita de forma errada continua sendo texto simples; o painel conta essas ocorrências.

## Adicionar marcas

- *Clique com o botão direito* no Modo de apresentação para abrir o círculo **Marcas de fala** com as marcas para escolher. O texto selecionado é envolvido por um par de marcas.
- `Ctrl+Shift+.` adiciona uma pausa, `Ctrl+Shift+,` um beat e `Ctrl+Shift+E` uma ênfase.
- **Configurações > Marcas de fala** permite criar suas próprias marcas e receitas (várias marcas em um só clique).

## Tempo, teleprompter, ensaio

O painel soma o tempo (palavras na sua velocidade, mais as pausas) em relação ao orçamento de cada seção. **Exibir > Teleprompter** abre uma janela que rola o texto no seu ritmo, e um ensaio registra quanto tempo cada parte realmente levou. **Copiar para IA** copia a apresentação com uma breve explicação das marcas. O teleprompter tem um tópico próprio no menu Ajuda.

## Pontos de partida

**Biblioteca > Modelos > Adicionar modelos iniciais** inclui um discurso (um brinde) e uma apresentação (uma palestra ou pitch) já marcados, em todos os idiomas do Caret.
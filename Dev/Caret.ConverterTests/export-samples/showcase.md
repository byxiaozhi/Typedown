---
title: Showcase of the Word export
author: Caret tests
keywords: [markdown, word, export]
---

# Showcase of the Word export

This document holds every construct the first phase of the Word export writes. It is read by the tests and, once per phase, opened in the real Word and looked at.

[TOC]

## Text

A paragraph with **bold**, *italic*, ***both***, ~~struck~~, `inline code`, H~2~O, x^2^, <u>underlined</u> text and a [link to a page](https://example.com/page). A bare address: https://example.org/path and a mail <ana@example.com>.

A hard break after this line  
goes on in the same paragraph. A single line break
in the source is a space.

A note follows this sentence.[^note] The [table below](#table) is linked from here, and a link to a heading that does not exist stays text: [nowhere](#nowhere).

Accents and scripts: Zażółć gęślą jaźń (Polish), ça va à l’été, où sont les œufs ? (French), ¿Qué tal, señor? ¡Muy bien! (Spanish), coração e ação (Portuguese).

## Lists

- First bullet
- Second bullet with **bold**
    - Nested bullet
    - Another nested one
        - Third level
- Back at the first level

1. First step
2. Second step
    1. A sub step
    2. Another sub step
3. Third step

- [x] A task that is done
- [ ] A task still open

An item with two paragraphs:

- The first paragraph of the item.

  The second paragraph, lined up under the text.
- The next item.

## Quote and code

> A quotation that is long enough to show how the bar in the margin and the gray text look in Word, with a second line that goes on.
>
> > A quote inside the quote moves in once more.

```csharp
public static int Add(int a, int b)
{
    return a + b;   // one paragraph per line
}
```

## Table

| Product | Quantity | Price |
| :------ | :------: | ----: |
| Apples | 3 | 1.20 |
| Pears | 12 | 14.40 |
| **Total** | **15** | **15.60** |

---

## Picture

![The track changes screen of Caret](docs/store/screenshots/en/1-track-changes.png)

<img src="docs/store/screenshots/en/2-teleprompter.png" alt="The teleprompter of Caret" style="zoom:40%;">

## Math

The roots of $ax^2 + bx + c = 0$ are $x = \frac{-b \pm \sqrt{b^2 - 4ac}}{2a}$, and $\alpha, \beta, \Gamma, \hbar$ are letters. A price of $5 stays text.

$$\sum_{i=1}^{n} i^2 = \frac{n(n+1)(2n+1)}{6}$$

$$\int_0^\infty e^{-x^2}\,dx = \frac{\sqrt{\pi}}{2}$$

$$A = \begin{pmatrix} a & b \\ c & d \end{pmatrix}, \quad f(x) = \begin{cases} x^2 & x \ge 0 \\ -x & x < 0 \end{cases}$$

$$\lim_{x \to 0} \frac{\sin x}{x} = 1, \quad \left[ \binom{n}{k} \right], \quad \hat{x}, \vec{v}, \overline{AB}$$

## Review and speech

The plan was {~~old~>new~~}{>>@Ana Pérez 2026-10-09<<} and we {++added++}{>>@Ana Pérez 2026-10-09<<} one step and {--removed--}{>>@Bo Dupont 2026-10-10<<} another. {==This sentence==}{>>@Bo Dupont 2026-10-10: Is this still true?<<} needs a check, and {==this==} is only highlighted.

Speech: Good evening {pause 2s} everyone, {slow}we begin slowly{/slow} {cue: slide one}.

[^note]: The text of a footnote, with **bold** and a [link](https://example.com/footnote).

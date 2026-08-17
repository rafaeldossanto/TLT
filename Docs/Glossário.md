---
tags: [glossario]
atualizado: 2026-08-17
---

# Glossário

**TLT** — sigla de *Transleitor*, o nome do produto.

**STT** (*speech-to-text*) — transcrição: áudio vira texto no mesmo idioma. Distinto
da tradução, que vem depois. Ver [[Transcrição e Tradução]].

**Loopback** — capturar o som que o PC está **enviando** para a saída, em vez de o
que entra pelo microfone. É o que permite legendar o que a outra pessoa fala na
chamada.

**WASAPI** — a API de áudio do Windows. Em modo loopback, entrega o áudio de
renderização de um dispositivo. Caminho nativo, sem driver virtual.

**VAD** (*voice activity detection*) — detecção de fala. Separa voz de silêncio,
música e ruído, evitando chamadas inúteis ao STT. Ver [[Pipeline de Áudio]].

**RTF** (*real-time factor*) — quantos segundos de áudio são processados por segundo
de relógio. RTF 12 significa transcrever 12 s de fala em 1 s. É a métrica que define
[[Requisitos de Hardware]].

**Janela deslizante** — reprocessar continuamente os últimos segundos de áudio, em
vez de transcrever cada trecho uma única vez. Compra latência baixa ao custo de
trabalho repetido.

**LocalAgreement-2** — a política que decide quando um texto para de ser provisório:
quando duas passagens consecutivas da janela concordam sobre o mesmo prefixo, ele é
confirmado.

**Segmento provisório / confirmado** — provisório ainda pode mudar (exibido em
cinza); confirmado não muda mais (branco) e é o único que vai para tradução.

**Overlay** — a janela de legenda sem borda, sempre no topo, sobreposta às demais.
Ver [[Interface e Overlay]].

**Whisper** — família de modelos de transcrição da OpenAI, com implementação em C++
(`whisper.cpp`) que roda localmente. Acessada aqui via Whisper.net.

**GGML / quantização** — formato e compressão dos pesos do modelo local. `q5_0`
reduz o tamanho com perda pequena de qualidade, permitindo modelo maior na mesma
VRAM.

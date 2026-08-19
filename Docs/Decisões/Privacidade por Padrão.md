---
tags: [decisao, adr]
atualizado: 2026-08-18
---

# Privacidade por Padrão

Decisão de 18/08/2026, tomada depois dos spikes de captura e de RTF. Consolida a
política de streaming e define o modo de operação padrão do produto.

## Decisão

**O TLT transcreve localmente por padrão. Nada de áudio sai da máquina sem escolha
explícita do usuário.**

A definição anterior era o inverso — nuvem como padrão de fábrica, local como opção
— e vinha de uma premissa que as medições derrubaram: a de que o modo local exigiria
hardware que o cliente médio não tem.

## O que tornou isso possível

`small` entrega **RTF 13,7x numa GTX 1050 Ti de 2016**, contra o alvo de 12 que a
janela deslizante pede. Se uma placa dessa geração sustenta, boa parte da base
instalada sustenta. Ver [[Requisitos de Hardware]].

A política de streaming fica confirmada como estava desenhada: **janela deslizante
com LocalAgreement-2**, mirando 1,5–3 s de latência. Ver [[Pipeline de Áudio]].

Aceleração por **Vulkan**, que roda com o driver comum, serve NVIDIA/AMD/Intel com um
binário só e dispensa CUDA Toolkit no cliente. Ver [[Decisões Deliberadas]].

## O furo que essa decisão expõe

> [!danger] Transcrever local não basta — a tradução também sai da máquina
> O desenho atual manda o texto transcrito para uma API de tradução. Esse texto **é**
> o conteúdo da conversa. Anunciar "o áudio não sai da sua máquina" enquanto a
> transcrição inteira viaja para um terceiro é uma meia-verdade que não sobrevive à
> primeira pergunta de um comprador corporativo — e com razão.

Ou a tradução também roda local, ou a promessa precisa ser reescrita para algo
honesto e mais fraco: "o áudio não sai, o texto sai".

Como a privacidade é a principal alavanca comercial do produto
(ver [[Posicionamento Comercial]]), a saída é **tradução local**: um modelo de
tradução automática rodando na máquina, no mesmo espírito do Whisper. Isso é escopo
novo, não previsto no plano original, e está registrado como task própria.

Enquanto não existir, o modo "privacidade total" simplesmente não está completo — e
não deve ser anunciado como se estivesse.

## Consequências

### Nunca cair para a nuvem automaticamente

Estava escrito que, se a nuvem falhasse, o app cairia para o local. Com a inversão do
padrão, o caminho perigoso é o oposto — e ele fica **proibido**:

> [!important] Degradação só desce, nunca sobe em exposição
> Se o modo local não sustentar (sem GPU, driver antigo, máquina fraca), o app
> degrada **dentro** do local: aumenta o intervalo de reprocessamento, depois cai
> para segmentação por frase, depois sugere um modelo menor. **Só então** oferece a
> nuvem — como pergunta, nunca como fallback silencioso.

Um fallback automático para a nuvem enviaria a conversa para um terceiro sem que o
usuário soubesse. Seria uma falha de privacidade, não um recurso de resiliência.

### O app tem que funcionar offline

A promessa "não sai da máquina" precisa ser **verificável pelo usuário**, não apenas
afirmada. No modo local o TLT deve operar com a rede desligada, sem degradar.

Isso vira critério de aceite e argumento de venda demonstrável: o cliente
desconfiado desliga o Wi-Fi e vê funcionando.

### Sem persistência por padrão

Áudio capturado e transcrições não são gravados em disco. Salvar é opt-in explícito,
por sessão. O app que promete privacidade não pode deixar o histórico da reunião de
ontem num arquivo temporário.

### Sem telemetria de conteúdo

Nenhuma métrica que carregue trecho de áudio, transcrição ou tradução. Se houver
telemetria, apenas técnica e agregada, com opt-in.

### O instalador engorda

Runtime Vulkan (58 MB) + modelo `small` (167 MB) + futuro modelo de tradução. O
modelo pode ser baixado na primeira execução em vez de embutido, mas note que
**mesmo esse download é um sinal de uso** — para o cliente mais exigente, embutir
tudo no instalador é o comportamento coerente com a promessa.

## Quando a nuvem entra

Continua existindo, como escolha consciente:

- Máquina sem GPU capaz, onde o local não alcança latência utilizável
- Idioma que o modelo local cobre mal
- Usuário que simplesmente prefere, e diz isso

Requisitos ao ativar: aviso claro do que passa a sair da máquina, indicador
permanente na interface enquanto estiver ativa, e teto de gasto configurável.

## Em aberto

- **Tradução local** — sem ela a promessa é incompleta (task criada)
- **RTF sob carga real** — as medições foram com GPU ociosa, e a folga de `small` é
  de 14%. Se cair abaixo do alvo durante uma chamada, o padrão pode ter que ser
  `base`. Ver task #12.
- **Qualidade com fala real** — o teste usou TTS, fácil demais. Ver task #11.

---
tags: [operacao]
atualizado: 2026-08-17
---

# Armadilhas Conhecidas

Erros mapeados **antes** de acontecer. Cada um destes custa de meio dia a uma semana
para quem descobre por conta própria.

## O `translate` do Whisper só traduz para inglês

O Whisper tem uma tarefa `translate` embutida, tentadora porque economizaria a etapa
de tradução inteira. Ela traduz **exclusivamente para o inglês** — não existe
parâmetro de idioma de destino.

Como o TLT traduz para português, ela é inútil aqui. Usar sempre `transcribe`
(devolve texto no idioma original) e deixar a tradução com o `ITranslator`. Ver
[[Transcrição e Tradução]].

## O overlay aparece no compartilhamento de tela

Compartilhou a tela na reunião, todos veem a legenda — inclusive a tradução do que
acabaram de dizer. Corrige-se com `SetWindowDisplayAffinity` +
`WDA_EXCLUDEFROMCAPTURE`. Ver [[Interface e Overlay]].

## O flag `Silent` não detecta silêncio

> [!danger] Não use `AudioClientBufferFlags.Silent` para pular processamento
> Medido em 18/08/2026: com 5 segundos de **silêncio digital absoluto** (100% de
> amostras em zero, confirmado por análise do WAV gravado), o WASAPI marcou o flag
> `Silent` em **zero buffers**. O flag existe, mas não é levantado no caso em que
> seria útil.

Quem confiar nele vai enviar silêncio ao STT achando que filtrou. A detecção de
silêncio tem que olhar o **conteúdo** — é trabalho do VAD, ou de uma checagem barata
de amostras zeradas antes dele. Ver [[Pipeline de Áudio]].

### O loopback continua entregando no silêncio

A crença comum — e o que estava escrito aqui antes — é que o loopback simplesmente
para de entregar buffers quando não há áudio. **Não foi o que aconteceu.** A captura
seguiu contínua durante o silêncio, entregando buffers de zeros, com intervalo máximo
de 35 ms entre eles.

> [!warning] Ressalva do teste
> Durante a medição havia um processo de áudio vivo na máquina, apenas sem
> reproduzir. O cenário de endpoint **totalmente ocioso**, sem nenhuma sessão de
> áudio aberta, não foi exercitado — e é justamente o estado do PC quando o TLT abre
> antes da chamada começar. Manter o tratamento defensivo: ausência prolongada de
> buffers é estado possível e não deve disparar reinício de captura.

## Trabalho pesado no callback de áudio

Detalhado em [[Pipeline de Áudio]], repetido aqui porque o sintoma engana: o áudio
corta de forma intermitente, sem exceção alguma no log. Quem não sabe da regra vai
procurar o bug no STT.

## Trocar o fone derruba a captura

Se o usuário troca o dispositivo de saída no meio da chamada — tira o fone, conecta
o Bluetooth — a captura morre silenciosamente. Detectar com `MMNotificationClient` e
reconectar no novo dispositivo padrão.

Pelo mesmo motivo, o resample precisa ser derivado do formato **real** do dispositivo
ativo. Taxa de amostragem hardcoded quebra no primeiro usuário com placa a 44,1 kHz.

## Recarregar o modelo a cada segmento

Custa segundos por chamada e destrói qualquer alvo de latência. Manter o contexto do
Whisper vivo entre segmentos.

## O Whisper.net cai para CPU sem avisar

`RuntimeOptions.RuntimeLibraryOrder` precisa ser configurado explicitamente, senão o
padrão é CPU mesmo com as DLLs de GPU presentes no diretório de saída. E quando a
carga da biblioteca acelerada falha — por dependência ausente, driver velho, o que
for — o fallback para CPU é **silencioso**: sem exceção, sem log, sem aviso.

O sintoma é o produto ficar dez vezes mais lento sem nenhum erro aparente.

Sempre verificar `RuntimeOptions.LoadedLibrary` após carregar o modelo, e expor isso
na interface de diagnóstico do app. O usuário que reclamar de lentidão precisa
conseguir dizer qual biblioteca está ativa.

## A primeira transcrição é muito mais lenta (Vulkan)

Medir sem descartar a primeira passada produz número errado: no Vulkan ela inclui
compilação de shaders. No spike da task #3, `base` apareceu **mais lento que**
`small` — impossível — só porque foi o primeiro a rodar.

Fazer uma passada de aquecimento curta e descartá-la antes de qualquer medição. No
app de produção, isso significa aquecer o modelo ao iniciar, e não deixar o custo
cair sobre a primeira frase da chamada do usuário.

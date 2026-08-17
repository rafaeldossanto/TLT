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

## O loopback fica mudo, não entrega zeros

No Windows 10, `WasapiLoopbackCapture` **não dispara** `DataAvailable` quando não há
absolutamente nenhum áudio tocando. O stream não entrega silêncio: ele simplesmente
para.

É estado normal, não falha. Reiniciar a captura ao detectar "ausência de dados" cria
um ciclo de reinício justamente quando ninguém está falando.

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

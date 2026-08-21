---
tags: [decisao, investigacao]
atualizado: 2026-08-18
---

# Tradução Local

Investigação aberta pelo ADR [[Privacidade por Padrão]]: sem tradução local, o texto
transcrito — que é o conteúdo da conversa — vai para uma API de terceiro, e a promessa
de privacidade não fecha.

## LLM generalista pequeno: descartado

Testado em 18/08/2026 com **Qwen2.5-3B-Instruct Q4_K_M** (1,8 GB) via LLamaSharp com
backend Vulkan, na GTX 1050 Ti. Traduzindo EN→PT frase a frase, com as três frases
anteriores como contexto.

**Latência: 2.232 ms de média** por frase (mediana 2.175, máximo 3.501).

> [!danger] A latência sozinha já elimina a opção
> A tradução roda **depois** do STT, então os tempos somam: 1,5–3 s de transcrição
> mais 2,2 s de tradução dá **4 a 5 segundos** ponta a ponta. É precisamente a
> latência que a janela deslizante foi desenhada para evitar. Não adianta discutir
> qualidade quando o orçamento de tempo já estourou.

E a qualidade também não passaria. Dois erros que **invertem o sentido**:

| Original | Saída | Problema |
|---|---|---|
| moving the service **off** the legacy database | "migração do serviço **para** a base legada" | inverteu a direção |
| ninety fifth **percentile** | "quinto quintil" | número completamente errado |
| mirrored cluster | "cluster refletido" | espelhado ≠ refletido |
| query load | "carregamento de solicitação" | sem sentido |

Somam-se erros de concordância ("o fila", "essa nó"), anglicismos ("flagrar",
"pushar") e `roadmap` traduzido como "roteiro" apesar da instrução explícita de
preservar termos técnicos.

**Conclusão:** um LLM generalista de 3B não é um bom tradutor. Ele é *quase* certo,
que num contexto de reunião de trabalho é pior que obviamente errado — o leitor não
tem como desconfiar de "para a base legada".

Modelo maior corrigiria parte da qualidade, mas pioraria a latência, que já é o fator
eliminatório. A direção está errada.

## Próxima hipótese: modelo especializado

Um modelo treinado **só** para tradução (família Opus-MT / Marian, arquitetura
encoder-decoder) é cerca de seis vezes menor (~300 MB contra 1,8 GB) e faz uma
passada muito mais curta que a geração autoregressiva de um LLM. Tende a ser mais
rápido **e** mais correto em tradução pura, justamente por não tentar ser generalista.

O obstáculo é de integração, não de modelo: em .NET, o tokenizer. Marian usa
SentencePiece, e não há binding tão pronto quanto o GGUF do LLamaSharp. Candidatos a
investigar: `Microsoft.ML.Tokenizers` (tem SentencePiece) sobre ONNX Runtime, ou
`Microsoft.ML.OnnxRuntimeGenAI`.

## Se nenhuma opção local servir

A consequência não é técnica, é de marketing: reescrever a promessa para "o áudio não
sai da sua máquina" e parar de vender privacidade total. Ver
[[Posicionamento Comercial]].

Melhor uma afirmação mais fraca e verdadeira do que uma forte que não sobrevive à
auditoria de um cliente.

## Opus-MT em ONNX: **viável**

Testado em 18/08/2026 com `R4kSo1997/opus-mt-en-pt-onnx-int8`, encoder-decoder Marian
quantizado em int8, rodando por ONNX Runtime em CPU. 337 MB contra 1,8 GB do Qwen.

Não foi preciso instalar Python: existe ONNX pré-exportado publicado no HuggingFace.

### Qualidade: resolve o problema que descartou o LLM

| Original | Qwen2.5-3B | Opus-MT |
|---|---|---|
| moving the service **off** the legacy database | "**para** a base legada" | "**do** banco de dados legado" |
| query load | "carregamento de solicitação" | "carga de consulta" |
| mirrored cluster | "cluster refletido" | "cluster espelhado" |
| message queue | "**o** fila" | "**a** fila de mensagens" |
| that node | "essa nó" | "esse nó" |

Os erros que **invertiam o sentido** desapareceram, e a concordância de gênero ficou
correta. É o resultado esperado de um modelo treinado só em pares de tradução, contra
um generalista de 3B.

Resta um erro observado: "ninety fifth percentile" virou "percentil noventa quinto
(...) noventa e cinco". Ruim, mas muito melhor que o "quinto quintil" do Qwen.

### Latência: 1.013 ms de média

Chegando lá em três passos, cada um medido:

| Versão | Média |
|---|---|
| decodificação gulosa sem cache | 1.709 ms |
| com cache de key/value | 1.307 ms |
| cache + argmax sobre buffer contíguo | **1.013 ms** |

O cache evita reprocessar a sequência inteira a cada token. O argmax pesava porque o
indexador de `Tensor<T>` recalcula o deslocamento a cada acesso, e são 54.776 posições
por token gerado — percorrer o buffer contíguo resolveu.

> [!important] O critério de 500 ms estava errado
> Ele foi definido supondo que o STT consumiria os 1,5–3 s inteiros do alvo. O STT
> ficou em **930 ms**, então sobra orçamento. A conta real é 930 + 1.013 ≈ **1,95 s
> ponta a ponta**, dentro do alvo do ADR.
>
> E o critério que de fato importa é outro: a tradução roda sobre texto **confirmado**,
> que sai a cada 1,5 s. Com 1.013 ms ela ocupa 67% desse intervalo e não acumula
> atraso.

### O que isso destrava

A promessa de [[Privacidade por Padrão]] pode fechar: transcrição **e** tradução na
máquina, com o app funcionando de rede desligada. Custo no instalador: 58 MB de
runtime Vulkan, 167 MB de Whisper `small` e 337 MB de Opus-MT.

Ainda por medir: o Opus-MT roda em CPU e o Whisper em GPU, então em princípio não
disputam — mas isso não foi verificado com os dois trabalhando ao mesmo tempo.

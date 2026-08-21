---
tags: [infraestrutura]
atualizado: 2026-08-18
---

# Empacotamento

Como o TLT sai da máquina de desenvolvimento e chega em outra.

```bash
powershell -ExecutionPolicy Bypass -File packaging\empacotar.ps1
```

Produz dois artefatos em `dist\`:

| Artefato | Tamanho | Para quem |
|---|---|---|
| `TLT-0.1.0-instalador.exe` | 70 MB | usuário final |
| `TLT-0.1.0-portatil.zip` | 107 MB | quem não quer instalar nada |

O ZIP é gerado sempre; o instalador só quando o Inno Setup está presente. Assim a
ausência da ferramenta não impede de produzir algo distribuível.

## Decisões da publicação

**Self-contained.** O usuário não precisa ter o .NET instalado. Exigir isso de quem só
quer legendar uma reunião perderia metade das instalações.

**Não é arquivo único.** O app carrega DLLs nativas grandes — o runtime Vulkan sozinho
tem 58 MB — e o modo arquivo único as extrairia para a pasta temporária **a cada
execução**. Como um instalador distribui isso de qualquer forma, uma pasta é melhor.

**ReadyToRun ligado.** O executável cresce, mas abre mais rápido, e a primeira
impressão de um app que já demora carregando modelos importa.

### Limpeza de runtimes de outras plataformas

> [!warning] O publish trouxe 73 MB de lixo na primeira tentativa
> Alguns pacotes copiam binários nativos de **todas** as plataformas, ignorando o
> `RuntimeIdentifier`. O primeiro publish deu 368 MB, dos quais 73 MB eram runtimes de
> macOS, Linux e ARM — em um aplicativo que só roda em Windows x64. O maior ofensor é
> o Whisper.net: o Vulkan para Linux sozinho ocupa 59 MB.

Um alvo de MSBuild remove essas pastas depois do publish, **por padrão de nome e não
por lista fixa**, para que um pacote novo trazendo outra plataforma também seja pego.
Resultado: 368 MB para 294 MB.

## Decisões do instalador

**Instala em `%LOCALAPPDATA%\Programs\TLT`, não em Program Files.** Dispensa
privilégio de administrador. O TLT é aplicativo de usuário, e exigir UAC de quem só
quer legendar uma reunião perde instalações sem ganhar nada.

**Os modelos não vão no instalador.** São cerca de 500 MB que o app baixa na primeira
execução. Embuti-los triplicaria o download para todo mundo, inclusive para quem só
quer experimentar.

**Exige Windows 10 build 19041.** É o mínimo das APIs usadas — captura por processo e
ocultação em compartilhamento de tela. Barrar na instalação é melhor que falhar em
execução.

**Na desinstalação, os modelos são preservados por padrão.** O desinstalador pergunta
se deve removê-los, e em modo silencioso simplesmente os mantém.

> [!tip] Um bug que só o teste completo pegou
> A primeira versão perguntava sobre os modelos **sempre**, inclusive em desinstalação
> silenciosa. O `/SUPPRESSMSGBOXES` do Inno Setup não suprime caixas de diálogo do
> código do próprio script, então uma desinstalação automatizada travava esperando um
> clique que ninguém daria.
>
> Corrigido com `UninstallSilent`, e o padrão no silêncio é **preservar**: apagar
> 500 MB sem confirmação seria a pior das duas escolhas.

## Ciclo verificado

Instalação silenciosa, presença em Programas Instalados, execução do app instalado,
desinstalação e preservação dos modelos — todos exercitados de ponta a ponta.

## Ainda não feito

- **Assinatura de código.** Sem certificado, o SmartScreen vai avisar sobre editor
  desconhecido na primeira execução. Para venda, isso precisa ser resolvido.
- **Atualização automática.** Hoje é baixar e instalar por cima.
- **Ícone e identidade visual** além do ícone atual, que representa as duas linhas do
  overlay.

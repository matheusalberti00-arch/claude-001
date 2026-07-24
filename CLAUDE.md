# CLAUDE.md — Contexto permanente do projeto

> Este arquivo é a **memória do projeto**. Qualquer sessão do Claude Code deve **ler
> isto primeiro** e respeitar estas regras, mesmo depois de o app ser fechado e reaberto.

## O que é este projeto

App **Android** que lê a balança de bioimpedância **Beurer BF 451** por Bluetooth (BLE),
mostra a composição corporal numa tela simples, e envia os dados direto para o
**Garmin Connect**. Substitui o app oficial Beurer HealthManager para esse fluxo.

Documentos irmãos: **`SPEC.md`** (o que o app faz) e **`PLAN.md`** (etapas de construção).
Leia os dois antes de trabalhar.

## O usuário

- É **leigo em programação** e depende do Claude para escrever e manter o código.
- Fala **português**. Toda comunicação deve ser em **português simples, sem jargão**.

## Restrição INEGOCIÁVEL: custo zero

- **Nenhum custo recorrente.** Sem servidor pago, backend na nuvem pago, API paga ou
  assinatura.
- Roda **100% no celular**: Bluetooth → balança → cálculo local → envio direto ao Garmin.
- Distribuição por **APK grátis** (Google Play, taxa única US$25, fica para o futuro).
- Se qualquer caminho exigir custo, **PARE e avise o usuário antes de seguir.**

## Decisões já tomadas (não reabrir sem o usuário pedir)

- **Tecnologia:** .NET MAUI (C#), espelhando o projeto de referência `mi-scale-exporter`
  (github.com/lswiderski/mi-scale-exporter).
- **Reaproveitar da referência:** parte 2 (cálculo de composição) e parte 3 (envio ao
  Garmin). Escrever nova apenas a parte 1 (leitura BLE da Beurer).
- **Protocolo Beurer:** basear-se em openScale (github.com/oliexdev/openScale) e
  ble-scale-sync. Modelos documentados: BF700/710/720/800/105. **O BF 451 NÃO está
  documentado** — é o maior risco do projeto (só confirma testando na balança real).
- **Garmin:** login (e-mail + senha) guardado **localmente e protegido no celular**;
  é o único caminho grátis. Nada de senha em servidor.
- **Multiusuário:** vários logins do Garmin; a pessoa **escolhe qual** antes de enviar.
  **Sem histórico de pesagens local** — quem guarda é o Garmin. Cada pesagem = um envio.
- **Dados:** sempre vêm da balança; o usuário **pode conferir e editar** antes de enviar.
- **Plataforma:** só Android na v1. Sem iOS.
- **Nome do app:** **CorpoSync** (evita as marcas Beurer/Garmin no que é distribuído).
  Id do pacote: `com.matheus.corposync`. Namespace/projeto: `CorpoSync`.
  "Beurer BF 451" só aparece em documentação, para descrever o hardware — nunca como
  nome do app. Nome de exibição é fácil de trocar; o id do pacote é fixo (trocá-lo
  força reinstalação limpa).

## Montagem do APK (build)

- Usuário usa **só o celular** — **sem PC**. APK montado de graça na nuvem via
  **GitHub Actions** (o projeto já está no GitHub: matheusalberti00-arch/claude-001).
- Fluxo: Claude envia código → GitHub monta o APK → usuário baixa pelo celular em
  **Actions/Releases** → instala. Dentro da cota grátis; se chegar perto do limite, avisar.

## Dados: balança → Garmin (todos aceitos pelo Garmin)

peso→`weight` · IMC→`bmi` · %gordura→`percent_fat` · %água→`percent_hydration` ·
músculo→`muscle_mass` · massa óssea→`bone_mass` · gordura visceral→`visceral_fat_rating` ·
BMR→`basal_met` · AMR→`active_met` · idade metabólica→`metabolic_age`.

### Confirmado pelo print do app oficial (BF 451, HealthManager Pro)

O app oficial exibe: Peso 64,9 kg · IMC 22,5 · Massa gorda 14,3% · Água 61,8% ·
Músculos 43,9% · Ossos 2,9 kg · BMR 1567 kcal · AMR 2899 kcal · Idade metabólica 25 ·
Gordura visceral 5,0 · (extras sem campo no Garmin: Soft Lean Mass, Lean Body Mass,
Proteína, Massa muscular c/ órgãos, Tecido adiposo subcutâneo).
- Os **10 primeiros** vão pro Garmin; os extras **não têm campo** no Garmin e ficam de fora.
- **Atenção de unidade:** balança mostra "Músculos" em **%**, Garmin guarda `muscle_mass`
  em **kg** → decidir conversão na Etapa 4 (provável: % × peso, ou usar valor de massa).

## Como trabalhar (regras de processo)

1. **Não escrever/editar código** enquanto o `PLAN.md` não estiver aprovado pelo usuário.
2. Seguir o `PLAN.md` **etapa por etapa**. Ao terminar uma etapa, **PARAR**, explicar em
   linguagem simples o que foi feito e **como testar**, e esperar aprovação antes da próxima.
3. Diante de qualquer **trade-off**, **explicar as opções em português simples e perguntar**
   — nunca decidir sozinho.
4. **Reaproveitar** bibliotecas/exemplos open-source testados em vez de escrever do zero.
5. Manter uma **rotina/modo de teste da balança** desde cedo (o BF 451 é o ponto incerto);
   comparar leituras com prints do app oficial Beurer.
6. Nunca mostrar erro técnico cru ao usuário — **mensagens humanas**.

## Estado atual do projeto

- ✅ Fase de planejamento: SPEC.md e PLAN.md escritos; PLAN revisado para build via
  GitHub Actions (usuário 100% no celular).
- ✅ Print do app oficial recebido e valores do BF 451 confirmados (ver acima).
- ✅ PLAN.md aprovado; **Etapa 1 CONCLUÍDA E APROVADA**: usuário instalou o app
  esqueleto no celular e a tela abriu (confirmado por print). Pipeline de build 100% ok.
- ✅ App renomeado para **CorpoSync** (a pedido do usuário, por causa das marcas).
- 🔧 **Etapa 2 PRÉ-PRONTA (aguarda teste na balança)**: tela de diagnóstico BLE com
  Plugin.BLE 3.2.1 — procura aparelhos, reconhece a balança pelo nome (bf/beurer/scale/
  balan), conecta e mostra os dados crus (hex) de cada canal notify. Build verde (run #6).
  **Ainda NÃO testado com a balança real** — o usuário vai testar quando estiver perto
  dela (e com um PC Windows disponível, opcional, para ver logs via adb logcat).
- ⏳ Próximo: usuário testa Etapa 2 com a balança; ajustar leitura conforme os dados
  crus + o print oficial; depois Etapa 3 (traduzir peso/impedância).
- Branch de trabalho: `claude/beurer-bf451-app-plan-gmfsj5`.
- Guia de teste da Etapa 2 para o usuário: `docs/COMO-TESTAR-ETAPA-2.md`.

### Notas técnicas de build (para futuras sessões)
- Alvo: **net10.0-android** (.NET 8 é EOL nesta data e o runner usa .NET 10).
- `dotnet workload install maui-android`. **Usar build `-c Release`** para o APK do
  celular: o Debug usa "fast deployment" e NÃO embute as assemblies, então o APK
  instalado manualmente abre e fecha na hora (crash). Release + EmbedAssembliesIntoApk=true
  gera APK completo e instalável.
- **Assinatura fixa de teste** em `build/corposync-test.keystore` (alias `corposync`,
  senha `corposync123`) para que atualizações instalem por cima sem desinstalar.
  É chave DESCARTÁVEL só para sideload — NUNCA usar para Play Store.
- O ambiente de planejamento (onde o Claude roda) NÃO consegue instalar .NET (política
  de rede bloqueia a Microsoft). Por isso o build é feito no GitHub Actions e o Claude
  acompanha via ferramentas MCP do GitHub.
- `MauiProgram` sem `AddDebug` (API removida no .NET 10);
  `SkipValidateMauiImplicitPackageReferences=true` para silenciar MA002.
- Workflow só roda em mudanças de `src/**` ou do próprio workflow (docs não disparam build).
- Download do APK: release `apk-latest` (repo é público, link direto funciona sem login).

## Referências

- Projeto base: https://github.com/lswiderski/mi-scale-exporter
- Protocolo Beurer: https://github.com/oliexdev/openScale · https://blescalesync.dev
- Campos aceitos pelo Garmin: https://github.com/cyberjunky/python-garminconnect

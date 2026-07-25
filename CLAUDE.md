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

### Mapa BLE do BF451 (lido do aparelho real na Etapa 2 — GATT)

Nome BF451, fabricante "Beurer GmbH", modelo BF451, serial FF03006D840D, bateria 100%,
relógio (1805/2a2b) já vem certo. É uma balança **padrão** de composição corporal com
**User Data Service** (reconhece usuário). Serviços/canais:
- 180a Device Info (2a29/2a24/2a26/2a27/2a28/2a25 = fabricante/modelo/fw/hw/sw/serial).
- 1805 Current Time: 2a2b [Read,Write,Notify].
- 181d Weight Scale: 2a9e (Feature)=31-00-00-00; **2a9d Weight [Indicate]**.
- 181b Body Composition: 2a9b (Feature)=CF-01-00-00; **2a9c Body Comp [Indicate]**.
- 181c User Data: 2a8c Gender[R/W], 2a85 Nascimento[R/W], 2a8e Altura[R/W],
  2a9a[R], 2a99 DB-Change[R/W/Notify], **2a9f User Control Point [Write,Indicate]**.
- ffff (vendor Beurer): fff0[R/W], fff1[W/Notify], fff2[R/W], fff3[R], fff4[W/Notify],
  fff5[Indicate], fff6[Indicate], fff7[W], fff8[W/Notify].

**Descoberta chave:** só "escutar" NÃO traz dados; a balança mostra "U--/👤?" (não
atribuiu a um usuário). Precisa do **handshake no 2a9f** (User Control Point): registrar
usuário (0x01 + código consentimento 2 bytes) → recebe índice; consentir (0x02 + índice +
código); depois gravar perfil (2a8c sexo, 2a85 nascimento aaaa/mm/dd, 2a8e altura cm) e
ouvir 2a9d/2a9c. Isso é a Etapa 3 (em teste com o usuário).
- Dados do usuário para teste (fixos no código por enquanto): sexo M, nasc 03/01/1996,
  altura 170 cm. Código de consentimento do app: 0x1717.

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
- ✅ **Etapa 2/3 (leitura + handshake)**: conecta, faz o handshake no 2a9f (registra/
  consente usuário), grava sexo/nascimento/altura e decodifica peso (2a9d) e composição
  (2a9c). Decodificação conferida com o print oficial (peso, BMR ~1577, massa magra 52,7).
- ✅ **Etapa 4 (tela bonita + perfis)**: tema escuro; "perfis favoritos" (dados do corpo +
  login Garmin) com CRUD; começa do zero (sem perfil "Eu"); mostra resultado em blocos.
- ❌ v0.5.3 (apagar+recriar usuário) FALHOU: o comando de apagar (UCP 0x03) fazia a
  balança mostrar "DEL" e NÃO pesar; ao subir de novo ela atribuía U1 ≠ do app (U3).
- ✅ **v0.6.1 — LEITURA AO VIVO RESOLVIDA**: removido o "apagar"; agora **reaproveita o
  usuário salvo (consentir 0x02)** e só registra novo se não houver; **ajusta o relógio
  (2a2b)** ao pesar; loga o usuário da pesagem (2a9c user id) e o contador 2a99.
  **CONFIRMADO pelo usuário:** pesou ao vivo (12:56, valor de agora) e a composição veio.
- ✅ **v0.6.0/0.6.2 — ENVIO PRO GARMIN CONFIRMADO**: enviou peso/IMC/gordura/água/músculo
  certinho pro Garmin Connect (conferido no app do Garmin). v0.6.2 deixa o envio mais leve:
  **desconecta da balança assim que a pesagem chega** e roda o upload fora da thread da UI
  (o celular travava durante o login do Garmin). 
- ✅ **v0.6.3 — ESPERAR A PESAGEM "DE AGORA"**: descoberto (pelo usuário) que a balança
  manda a pesagem GUARDADA primeiro e a AO VIVO (bioimpedância) alguns segundos depois; a
  v0.6.2 desconectava 4s após a 1ª (guardada) e cortava a boa. Agora o app **só encerra/
  desconecta quando chega uma pesagem com carimbo ~agora** (dentro de 3 min do relógio do
  celular); enquanto isso mostra "aguardando a de agora". Também **não regrava sexo/nasc/
  altura** ao reaproveitar usuário (menos interação). **PRECISA TESTE.**
- ❌ v0.6.4 (reconectar em loop p/ sincronizar) FALHOU: o log (decodificado) provou que
  TODA reconexão baixa o MESMO pacote 2a9d, byte a byte, com carimbo 12:56:22 — a balança
  só reenvia a última guardada e NUNCA uma nova. Pior: conectar/desconectar toda hora parece
  ATRAPALHAR a balança a completar e guardar a pesagem nova (por isso a última guardada
  ficou congelada em 12:56).
- 🔑 **v0.6.5 — MEÇA PRIMEIRO, BAIXE DEPOIS (escolha do usuário)**: novo fluxo, igual ao app
  oficial/openScale. O usuário **sobe na balança e deixa ela terminar TUDO sozinha** (sem o
  app grudado), a balança guarda; **depois** toca em "Pesar" e o app conecta **uma vez**,
  consente o usuário e **baixa a pesagem de data mais nova** (a balança despeja as guardadas
  ao conectar; o app fica com a de maior carimbo de hora), mostra e desconecta. Removido o
  loop de reconexão e o `AgendarDesconexao`; `FluxoLeituraAsync` virou `BaixarPesagemAsync`
  (coleta ~8s). **PRECISA TESTE.** Se ainda vier antiga, o próximo é o canal vendor `ffff`.
- ❌ v0.6.5 (medir primeiro, baixar depois) FALHOU: log de 14:12 provou que a balança
  ainda entrega só a de 12:56 mesmo pesando antes; a pesagem nova NÃO é gravada no
  usuário U3 (pacotes 2a9d/2a9c não trazem user id, então não dá p/ saber onde caiu).
- ✅ **v0.6.6/0.6.7 — LEITURA AO VIVO RESOLVIDA DE VERDADE (confirmado pelo usuário)**: o
  modo captura revelou a solução real. A balança **"desfila" TODAS as pesagens guardadas ao
  conectar, das antigas para as novas**, pelo canal PADRÃO (2a9d/2a9c); a de AGORA é gravada
  durante a conexão e chega no fim do desfile. Bastava **ficar conectado enquanto o usuário
  pesa** (sem desconectar cedo, sem reconectar) e guardar a de DATA MAIS NOVA. Confirmado:
  usuário "Teste" e "Matheus" puxaram até a pesagem de agora (14:28, 65,4 kg). O canal
  secreto `fff` **NÃO era necessário** (erramos o diagnóstico antes). v0.6.7 = versão limpa:
  removido o modo captura/fff; fluxo `LerPesagemAsync` fica conectado ~1 min (para assim que
  chega carimbo ~agora) e mostra a mais nova. **PENDENTE confirmar o v0.6.7 limpo no teste.**
- 🔁 **v0.6.7 — Garmin mais robusto**: o envio deu `OAuthToken2IsNull` (instabilidade do
  login Garmin). Agora `GarminService` **tenta até 3x** em falhas passageiras (tokens OAuth
  nulos/CSRF/service ticket), limpa espaços do e-mail/senha e traduz o erro. **PRECISA TESTE.**
- ⚠️ Pendências: decodificar massa óssea, gordura visceral, idade metabólica — suspeita no
  pacote do canal `fff6` (`05-00-1C-48-...`), que não decodificamos (fica para depois).
- ✅ **Etapa 5 (envio pro Garmin) — v0.6.0**: `GarminService.cs` usando a lib
  **YetAnotherGarminConnectClient 0.0.17** (login e-mail+senha, 2FA, upload). Botão
  "Enviar pro Garmin" na tela de resultado usa o login do perfil ativo. Manda peso,
  gordura%, água% (de kg), músculo kg (de %) e IMC. **Não testado com conta real ainda.**
- ⏳ Próximo (de manhã, com o usuário): testar leitura ao vivo (v0.5.3) e o envio ao
  Garmin (criar perfil com login real). Depois decodificar os campos que faltam
  (massa óssea, gordura visceral, idade metabólica) e completar o envio.
- Branch de trabalho: `claude/beurer-bf451-app-plan-gmfsj5`.
- Guias de teste: `docs/COMO-TESTAR-ETAPA-2.md`, `docs/RESUMO-DA-MANHA.md` e a pesquisa
  em `docs/PESQUISA-BEURER-E-GARMIN.md`.
- **Achado de pesquisa:** a BF451 é balança PADRÃO (Body Composition + User Data Service),
  igual às BF105/600/850/950 do openScale — NÃO precisa do canal proprietário `ffff`. O
  histórico vem pelos canais padrão após reconhecer o usuário; o contador 2a99 (Database
  Change Increment) é o próximo experimento se o v0.5.3 não resolver.

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

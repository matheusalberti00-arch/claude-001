# SPEC — App Beurer BF 451 → Garmin Connect

> Documento de **especificação** em linguagem simples. Descreve **o que** o app faz.
> Não é código. A parte técnica de **como** construir fica no arquivo `PLAN.md`.
> Versão deste documento: rascunho para aprovação (v1).

---

## 1. Em uma frase

Um app de celular Android que lê os dados da balança **Beurer BF 451** por Bluetooth,
mostra o resultado numa tela bonita e simples, e envia esses dados direto para o
**Garmin Connect** — sem abrir o app oficial da Beurer e sem digitar nada à mão.

---

## 2. Para quem é

- Uso **pessoal e da família** (várias pessoas usam a mesma balança e o mesmo app).
- O app guarda **um login do Garmin para cada pessoa**. Antes de enviar, a pessoa
  **escolhe qual login** vai receber a pesagem.
- O app **NÃO guarda histórico** de pesagens de ninguém. Ele lê, mostra, envia, e pronto.
  Quem guarda o histórico é o próprio Garmin.

---

## 3. Como vai funcionar no dia a dia (a experiência)

1. A pessoa abre o app.
2. Aperta um botão grande tipo **"Pesar"**.
3. Sobe na balança Beurer.
4. O app recebe os dados por Bluetooth e mostra na tela (peso, gordura, água, etc.).
5. A pessoa **escolhe o login do Garmin** (o seu, o do cônjuge, etc.).
6. Confere os números. Se quiser, **edita** algum valor.
7. Aperta **"Enviar para o Garmin"**.
8. O app confirma: **"Enviado! ✅"**.

Cada vez que sobe na balança e envia = **um envio** para o Garmin.

---

## 4. Restrição inegociável: custo zero

- **Nenhum** custo recorrente. Sem servidor pago, sem backend na nuvem, sem API paga,
  sem assinatura.
- Tudo roda **100% no celular**: Bluetooth do celular → balança → cálculo local →
  envio direto para o Garmin.
- Instalação por **arquivo APK grátis** (a loja Google Play, com sua taxa única de
  US$25, fica para o futuro, se um dia você quiser).
- Se em qualquer ponto surgir a necessidade de algo pago, **o desenvolvimento para e
  te avisa antes de seguir**.

---

## 5. Quais dados a balança mede e para onde vão no Garmin

A Beurer BF 451 mede vários valores. O Garmin aceita quase todos eles. Mapa completo:

| A balança Beurer mede | Vai para o Garmin como | Aceito? |
|---|---|---|
| Peso | `weight` | ✅ |
| IMC (calculado da altura) | `bmi` | ✅ |
| % de gordura corporal | `percent_fat` | ✅ |
| % de água corporal | `percent_hydration` | ✅ |
| Massa/percentual muscular | `muscle_mass` | ✅ |
| Massa óssea | `bone_mass` | ✅ |
| Gordura visceral | `visceral_fat_rating` | ✅ |
| Taxa metabólica basal (BMR) | `basal_met` | ✅ |
| Taxa metabólica ativa (AMR) | `active_met` | ✅ |
| Idade metabólica | `metabolic_age` | ✅ |

> **Observação honesta:** a lista exata de valores que o **BF 451** entrega pelo
> Bluetooth só será 100% confirmada quando testarmos com a balança de verdade (ver
> seção 8). Se algum valor não vier pronto da balança, o app pode **calculá-lo**
> localmente a partir do peso + impedância + (altura, idade, sexo). Nenhum valor
> medido pela balança será descartado se o Garmin tiver um campo para ele.

---

## 6. Telas do app (v1)

Simples de propósito. Quatro telas, no máximo.

### Tela 1 — Início ("Pesar")
- Um botão grande **"Pesar"**.
- Uma indicação simples do estado do Bluetooth ("Procurando balança…", "Suba na
  balança", etc.), sem jargão técnico.
- Um cantinho discreto para **Configurações**.

### Tela 2 — Resultado da pesagem
- Os números lidos, bem grandes e legíveis (peso em destaque).
- Um seletor **"Enviar como:"** com a lista de pessoas/logins cadastrados.
- Cada valor pode ser **tocado e editado** (caso queira corrigir algo).
- Botão **"Enviar para o Garmin"**.

### Tela 3 — Configurações
- Lista de **perfis/logins do Garmin** cadastrados (adicionar, remover).
- Ao adicionar: campos de **e-mail e senha do Garmin** + os dados usados no cálculo
  quando necessário (**altura, idade, sexo**).
- Aviso claro de que a senha **fica só no celular**.

### Tela 4 — Confirmação
- Mensagem simples: **"Enviado com sucesso ✅"** ou **"Não deu certo, tentar de novo"**
  com uma explicação em linguagem humana (nunca um erro técnico cru).

---

## 7. Como funcionam as 3 partes por dentro

O app tem 3 "motores". As partes 2 e 3 são **reaproveitadas** do projeto de referência
open-source `mi-scale-exporter` (escrito em .NET MAUI / C#). Só a parte 1 é feita nova
para a Beurer.

### Parte 1 — Ler a balança (Bluetooth / BLE) — **NOVA, específica da Beurer**
- O celular conecta na balança por **Bluetooth Low Energy (BLE)**.
- Recebe **peso + impedância** (e, se a balança enviar, os valores já calculados).
- Baseado no protocolo Beurer documentado pelos projetos abertos **openScale** e
  **ble-scale-sync** (modelos BF700/710/720/800/105).
- **Risco conhecido:** o BF 451 não está nessas listas; pode precisar de ajuste. Por
  isso existe a "rotina de teste" da seção 8.

### Parte 2 — Calcular a composição corporal — **REAPROVEITADA**
- A partir de peso + impedância + (altura, idade, sexo), calcula os percentuais.
- Só é usada **se** a balança não entregar o valor pronto. Se a balança já mandar o
  número, o app usa o da balança.

### Parte 3 — Enviar para o Garmin Connect — **REAPROVEITADA**
- O app entra na conta do Garmin escolhida (e-mail + senha guardados no celular) e
  envia a pesagem como um registro de composição corporal.
- **Sem servidor no meio.** É o celular falando direto com o Garmin.
- A senha fica guardada de forma protegida **no próprio aparelho**.

---

## 8. Rotina de teste da balança (parte do escopo, a seu pedido)

Como o BF 451 é um modelo não documentado, o plano inclui, **desde cedo**, uma forma
simples de testar e confirmar a leitura:

- Uma **tela/modo de diagnóstico** escondido (ou um app-teste separado bem pequeno) que
  mostra os dados "crus" que a balança envia, para eu conferir se batem com o que o app
  oficial da Beurer mostra.
- Você me manda um **print do app oficial** (Beurer HealthManager) logo após uma
  pesagem, e comparamos.
- Um passo-a-passo simples de "como testar" acompanha **cada etapa** do `PLAN.md`, para
  você conseguir aprovar antes de eu seguir.

---

## 9. O que está FORA de escopo nesta primeira versão (v1)

Para manter simples e entregável, a v1 **não** terá:

- Histórico/gráficos de pesagens dentro do app (quem guarda é o Garmin).
- Suporte a outras marcas de balança além da Beurer BF 451.
- Suporte a iPhone/iOS (só Android nesta versão).
- Publicação na Google Play (fica para depois; v1 é APK).
- Envio para outros serviços além do Garmin (ex.: Apple Health, Fitbit).
- Contas/login próprios do app (o único login é o do Garmin).
- Sincronização automática em segundo plano (o envio é sempre com você apertando o botão).
- Vários idiomas (v1 em português).

---

## 10. Decisões já tomadas (registradas)

- **Tecnologia:** .NET MAUI (C#), igual à referência, para reaproveitar cálculo + envio
  ao Garmin já prontos.
- **Distribuição:** APK grátis.
- **Garmin:** login (e-mail + senha) guardado localmente no celular; único caminho grátis.
- **Multiusuário:** vários logins do Garmin; a pessoa escolhe antes de enviar; sem
  histórico local.
- **Dados:** sempre vêm da balança; o usuário pode conferir e editar antes de enviar.
- **Risco aceito:** o BF 451 pode exigir testes/ajustes extras no Bluetooth — sem custo,
  só tempo.

---

## 11. Riscos e pontos em aberto

| Risco / dúvida | Como vamos lidar |
|---|---|
| BF 451 pode falar um "dialeto" BLE diferente | Rotina de teste (seção 8) desde o começo |
| Login do Garmin pode mudar/quebrar no futuro | Usamos a mesma técnica da referência, que é mantida pela comunidade; se quebrar, atualizamos |
| Quais valores exatos o BF 451 envia | Confirmar com print do app oficial + teste real |
| Permissões de Bluetooth no Android moderno | Tratadas no app com telas de permissão simples |

---

*Próximo passo depois de você aprovar este SPEC: eu escrevo o `PLAN.md` com as etapas
pequenas de implementação, cada uma testável e aprovável por você antes da próxima.*

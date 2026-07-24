# PLAN — Plano técnico de implementação

> Etapas **pequenas**, em ordem. Cada uma tem um **"Como testar"** para você aprovar
> antes de eu seguir para a próxima. Regra do projeto: **nada da etapa seguinte começa
> antes de você aprovar a etapa atual.**
>
> Base: SPEC.md. Tecnologia: **.NET MAUI (C#)**, reaproveitando cálculo + envio ao
> Garmin do projeto `mi-scale-exporter`.

Legenda de risco: 🟢 tranquilo · 🟡 atenção · 🔴 parte incerta (BF 451)

---

## Fase 0 — Preparar o terreno

### Etapa 1 — App "esqueleto" que instala no celular 🟢
**O que faço:** crio o projeto MAUI mínimo (uma tela em branco escrito "Beurer → Garmin")
e gero um **APK**.
**Por que primeiro:** garante que o caminho de "escrever → gerar APK → instalar no seu
celular" funciona, antes de complicar.
**Como você testa:**
1. Você recebe um arquivo `.apk`.
2. Instala no celular Android (te passo o passo-a-passo de "fontes desconhecidas").
3. O app abre e mostra a tela em branco. ✅
**Pronto quando:** o app abre no seu celular.

---

## Fase 1 — Ler a balança (a parte incerta, atacada cedo)

### Etapa 2 — Modo diagnóstico: enxergar a balança 🟡
**O que faço:** uma tela escondida de "diagnóstico" que procura aparelhos Bluetooth por
perto, acha a **Beurer BF 451**, conecta e mostra os **dados crus** (os bytes) que ela
manda quando você pesa.
**Como você testa:**
1. Abre o modo diagnóstico, sobe na balança.
2. Aparecem linhas de dados crus na tela.
3. Você tira um **print do app oficial (Beurer HealthManager)** logo após pesar, e me
   manda junto com os dados crus. ✅
**Pronto quando:** conseguimos ver, de forma repetida, os dados que a balança envia.

### Etapa 3 — Traduzir os dados da Beurer (peso + impedância) 🔴
**O que faço:** interpreto aqueles bytes crus para virar números de verdade (peso,
impedância e o que mais vier), me baseando no protocolo Beurer dos projetos abertos
(openScale / ble-scale-sync).
**Aqui pode haver ajuste:** se o BF 451 for diferente, a gente itera nesta etapa com base
nos seus prints. **Sem custo, só tempo.**
**Como você testa:**
1. Sobe na balança.
2. O **peso** mostrado no app bate com o peso real / com o app oficial.
3. Confirmamos que a impedância e os outros valores aparecem. ✅
**Pronto quando:** o peso (no mínimo) sai certo e estável.

---

## Fase 2 — Calcular a composição

### Etapa 4 — Preencher todos os valores 🟡
**O que faço:** trago a lógica de cálculo da referência (parte 2). Onde a balança já
manda o valor pronto, uso o dela; onde não manda, calculo a partir de peso + impedância +
(altura, idade, sexo).
**Como você testa:**
1. Sobe na balança.
2. Os valores (gordura, água, músculo, óssea, visceral, metabólica…) aparecem e **batem,
   dentro de uma pequena margem, com o app oficial**. ✅
**Pronto quando:** os números fazem sentido comparados ao app da Beurer.

---

## Fase 3 — Enviar para o Garmin

### Etapa 5 — Cadastro de perfis/logins do Garmin 🟢
**O que faço:** a tela de Configurações onde você adiciona pessoas (e-mail + senha do
Garmin, e altura/idade/sexo). A senha é guardada **protegida, só no celular**.
**Como você testa:**
1. Adiciona um login.
2. Fecha e reabre o app.
3. O login continua lá; a senha não aparece escrita em lugar nenhum. ✅
**Pronto quando:** dá pra cadastrar e remover perfis, e eles persistem.

### Etapa 6 — Enviar uma pesagem de teste ao Garmin 🟡
**O que faço:** trago o envio ao Garmin (parte 3) e mando uma pesagem (primeiro com
valores fixos de teste) para a conta escolhida.
**Como você testa:**
1. Apara "enviar teste".
2. Você abre o **Garmin Connect** (app ou site) e **vê a pesagem lá**. ✅
**Pronto quando:** o dado de teste chega no Garmin certo.

---

## Fase 4 — Juntar tudo numa experiência bonita

### Etapa 7 — Fluxo completo de verdade 🟡
**O que faço:** ligo tudo: Tela Início com botão **"Pesar"** → lê a balança → Tela
Resultado com os números → escolher o **login** → **editar** se quiser → **"Enviar para
o Garmin"** → Tela de confirmação.
**Como você testa:**
1. Faz o fluxo inteiro de ponta a ponta, pesando de verdade.
2. O dado real aparece no Garmin da pessoa certa. ✅
**Pronto quando:** dá pra usar do começo ao fim sem instruções técnicas.

### Etapa 8 — Acabamento: bonito, permissões e mensagens humanas 🟢
**O que faço:** deixo as telas bonitas e modernas, trato as permissões de Bluetooth com
telas simples, e troco qualquer erro técnico por mensagens em linguagem humana.
**Como você testa:**
1. Usa o app como um usuário leigo.
2. Nada técnico/feio aparece; erros são compreensíveis. ✅
**Pronto quando:** você acha o app agradável e claro.

---

## Fase 5 — Entrega

### Etapa 9 — APK final + guia de instalação 🟢
**O que faço:** gero o APK final e escrevo um guia curtinho de instalação/uso.
**Como você testa:**
1. Instala o APK final num celular "limpo".
2. Cadastra um login, pesa, envia, confirma no Garmin. ✅
**Pronto quando:** funciona do zero, sem minha ajuda ao lado.

---

## Ordem e dependências (resumo visual)

```
Etapa 1 (esqueleto)
   └─ Etapa 2 (ver balança) ── Etapa 3 (traduzir dados) ◄── parte mais incerta
                                     └─ Etapa 4 (calcular)
                                            └─ Etapa 5 (logins Garmin)
                                                   └─ Etapa 6 (enviar teste)
                                                          └─ Etapa 7 (fluxo completo)
                                                                 └─ Etapa 8 (acabamento)
                                                                        └─ Etapa 9 (APK final)
```

## Regras que valem em todas as etapas

- **Só começo a etapa seguinte depois da sua aprovação.**
- No fim de cada etapa, **paro e explico em linguagem simples** o que foi feito e como testar.
- Se surgir uma decisão com **trade-off**, eu **explico as opções e pergunto** — não decido sozinho.
- Sempre que possível, **reaproveito bibliotecas/exemplos open-source já testados** em vez
  de escrever do zero.
- Se algo exigir **custo**, eu **paro e aviso antes**.

---

*Quando você aprovar este PLAN.md, começo pela **Etapa 1** — e só a partir daí escrevo
código.*

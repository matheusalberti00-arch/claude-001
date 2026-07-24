# Bom dia! Resumo do que fiz enquanto você dormia ☀️

## O que tem de novo (versão 0.6.0)

### 1. 🎯 Agora dá pra ENVIAR PRO GARMIN (a peça que faltava!)
- Apareceu um botão **"Enviar pro Garmin"** na tela de resultado, embaixo dos dados.
- Ele usa o **e-mail e a senha do Garmin do perfil que você escolheu**.
- Se o Garmin pedir um **código de verificação (2FA)**, o app abre uma janelinha
  pedindo o código — é só digitar.
- A senha continua **só no cofre do celular**. Nada vai pra servidor nenhum.
- Usei uma biblioteca pronta e gratuita (a mesma família do app de referência), então
  não precisei inventar o login do zero.

### 2. 🔬 Continua o teste da leitura ao vivo (o problema da pesagem antiga)
- Lembra que a balança sempre trazia a pesagem **velha** (das 20:58)? Pesquisei bastante
  e descobri uma coisa boa: **nossa balança é "padrão"**, igual às Beurer que o openScale
  já suporta. Ou seja, **estamos no caminho certo** — não precisa de nada secreto.
- A versão que você vai baixar já tenta a correção: **cria um usuário novo na balança a
  cada pesagem** (a teoria é que assim ela é obrigada a mandar a pesagem de agora, não uma
  guardada). **Esse é o teste principal da manhã.**

## ⚙️ Como testar (passo a passo)

### Teste A — a leitura (o mais importante)
1. Baixe a versão nova no celular (link mais abaixo) e instale por cima.
2. Abra o app, crie/escolha um perfil e toque em **Pesar**.
3. Quando aparecer **"SUBA AGORA"**, suba na balança e fique parado.
4. Veja no resultado: **a hora da pesagem bate com o horário de agora?**
   - Se aparecer **"Pesagem agora (HH:mm)"** com o horário certo → 🎉 resolvemos!
   - Se ainda vier uma hora antiga → sem problema, testo o próximo plano (ouvir o
     contador 2a99). Me manda um print da tela de **"Detalhes técnicos"**.

### Teste B — o envio pro Garmin
1. Toque em **Perfis** → edite seu perfil → preencha **e-mail e senha do Garmin** → Salvar.
2. Faça uma pesagem (Teste A).
3. Toque em **"Enviar pro Garmin"**.
   - Se pedir código (2FA), digite o que chegou por SMS/app/e-mail.
   - Deve aparecer **"Enviado pro Garmin com sucesso! ✅"**.
4. Confira no app/site do Garmin Connect se a pesagem apareceu.
   - ⚠️ **Aviso honesto:** o login do Garmin é complicado e **não consegui testar sem a
     sua conta**. Pode ser que precise de 1 ou 2 ajustes. Se der erro, me manda a mensagem
     que apareceu (ela já vem em português) que eu corrijo rapidinho.

## 📥 Onde baixar
Link direto (funciona sem login, repositório é público):
`https://github.com/matheusalberti00-arch/claude-001/releases/download/apk-latest/com.matheus.corposync-Signed.apk`

## 📌 O que ainda falta (pra gente ver junto depois)
- Confirmar a leitura ao vivo (Teste A) — se não der, tenho os próximos planos prontos.
- Confirmar o envio ao Garmin (Teste B).
- Alguns dados que o app oficial mostra (massa óssea, gordura visceral, idade metabólica)
  a gente ainda não "traduziu" dos códigos da balança — dá pra completar depois que a
  leitura ao vivo estiver 100%.
- Sobre o músculo: a balança dá em **%** e o Garmin guarda em **kg**; por enquanto
  converto multiplicando pelo peso. Podemos revisar esse número juntos.

Qualquer coisa, é só me chamar. 🙂

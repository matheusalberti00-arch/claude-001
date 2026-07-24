# Como testar a Etapa 2 (encontrar a balança) — guia rápido

> Objetivo desta etapa: ver o app **encontrar sua balança BF 451 no Bluetooth**,
> conectar, e **mostrar os dados crus** que ela envia quando você se pesa.
> É um "modo diagnóstico" — os números ainda vêm em código (hex); traduzir vem na Etapa 3.

## O que você precisa
- Seu celular Android (o mesmo de sempre).
- Estar **perto da balança**.
- Bluetooth **ligado** no celular.
- (Opcional) Seu PC Windows, só se quisermos ver detalhes técnicos — **não é obrigatório**.

## Passo 1 — Instalar o app CorpoSync
1. **Desinstale o app antigo "Beurer Garmin"** (aquele da Etapa 1), se ainda estiver lá.
   - Segure o ícone → Desinstalar. *(Só desta vez; o nome mudou para CorpoSync.)*
2. Baixe o novo APK (toque pelo celular):
   `https://github.com/matheusalberti00-arch/claude-001/releases/download/apk-latest/com.matheus.corposync-Signed.apk`
   - Ou pela página: `github.com/matheusalberti00-arch/claude-001/releases/latest` (arquivo `.apk` em **Assets**).
3. Abra o arquivo e instale (autorize "fonte desconhecida" se pedir).

## Passo 2 — Rodar o diagnóstico
1. Abra o app **CorpoSync**.
2. Toque em **"Procurar balança"**.
3. Na primeira vez, o Android vai **pedir permissão de Bluetooth** → toque em **Permitir**.
4. **Suba na balança** (ligue ela subindo/pisando, como você faz normalmente).
5. Observe a tela: vão aparecer linhas com os aparelhos encontrados. Quando achar a
   balança, o app tenta **conectar sozinho** e começa a mostrar linhas **"DADO ..."**.

## O que me mandar (é isso que destrava a Etapa 3)
- Um **print da tela do CorpoSync** mostrando as linhas (principalmente as **"DADO ..."**).
- Se possível, faça **uma pesagem normal no app oficial Beurer logo em seguida** e me diga
  o **peso** que ele mostrou — assim eu comparo com os dados crus e descubro onde está o peso.

## Se algo não acontecer
- **Não aparece nada / não acha a balança:** confirme que o Bluetooth está ligado, que você
  autorizou a permissão, e que a balança "acordou" (suba nela). Me manda um print mesmo assim.
- **Aparecem aparelhos, mas a balança não conecta:** me manda o print — o nome dela pode ser
  diferente do que o app espera, e eu ajusto num minuto.
- **O app fechar:** print da tela; eu puxo o relatório e corrijo.

## (Opcional) Usando o PC Windows para ver detalhes
Só se você quiser — ajuda a depurar casos difíceis:
1. Ative no celular: Configurações → "Opções do desenvolvedor" → **Depuração USB**.
2. No Windows, instale o "SDK Platform Tools" (adb) da Google (grátis).
3. Ligue o celular no PC por cabo e rode no terminal: `adb logcat | findstr CorpoSync`
4. Isso mostra mensagens técnicas ao vivo. **Não precisa** disso pra testar — é só um extra.

---
*Quando você me mandar os prints, seguimos para a Etapa 3: traduzir esses dados crus em
peso e impedância de verdade.*

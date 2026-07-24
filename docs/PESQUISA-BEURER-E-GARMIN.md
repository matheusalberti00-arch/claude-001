# Pesquisa: como outros apps leem a Beurer e mandam pro Garmin

> Anotações da noite de 24/07, pra guiar as próximas correções. Linguagem simples.

## Parte 1 — Leitura da balança Beurer

### O achado mais importante
As Beurer com Bluetooth que o **openScale** (app open-source famoso) suporta —
BF105, BF600, BF850, BF950 — **NÃO usam um protocolo secreto próprio**. Elas são
**balanças "padrão"** do Bluetooth: usam o *Body Composition Service* (181b) + o
*User Data Service* (181c), exatamente os mesmos canais que a **nossa BF451 tem**
(confirmado no mapa GATT que lemos do aparelho).

**Conclusão:** estamos no caminho certo. Não precisamos "quebrar" o canal
proprietário `ffff` da Beurer. O histórico que o app oficial "puxa" vem pelos
**mesmos canais padrão** (2a9d peso / 2a9c composição), depois que a balança
reconhece o usuário.

Fonte: openScale, PR #753 (suporte às BF105/BF600/BF850/BF950) e o driver padrão
`BluetoothStandardWeightProfile`.

### Como a leitura funciona (modelo mental)
1. A balança guarda as pesagens **por usuário** (ela tem 8 espaços: U1..U8).
2. O app registra/consente um usuário pelo **User Control Point (2a9f)** — que é
   exatamente o handshake que já fazemos (0x01 registrar, 0x02 consentir).
3. Depois de reconhecer o usuário, a balança **entrega as pesagens** daquele
   usuário como *indications* em 2a9d (peso) e 2a9c (composição).
4. Existe um contador, o **Database Change Increment (2a99)**, que a balança usa
   pra marcar "tem pesagem nova". O app oficial provavelmente usa isso pra saber
   o que já baixou e o que é novo.

### Por que ainda pegamos a pesagem ANTIGA (o problema atual)
A balança parece reenviar a **última pesagem guardada daquele usuário**, não a
que acabou de acontecer. Hipóteses (da mais provável pra menos):

- **(A) — em teste agora (v0.5.3):** se o usuário for **novo em folha** (sem
  histórico), a única pesagem que a balança pode mandar é a **ao vivo**. Por isso
  passamos a **apagar e recriar** o usuário a cada pesagem. → **Você testa isso.**
- **(B) Ordem das pesagens:** a balança pode mandar **várias** pesagens guardadas
  em sequência e a ao vivo por último. Já ficamos com a de **carimbo de hora mais
  recente** (`_melhorQuando`). Mas nos testes só apareceu **uma** linha 2a9d, o que
  reforça a hipótese A (usuário tinha só 1 pesagem guardada).
- **(C) Contador 2a99:** ouvir o 2a99 e reagir a ele pode ser o gatilho certo pra
  a balança soltar a pesagem nova. É o próximo experimento se A e B não bastarem.

### Próximos passos possíveis na leitura (se o v0.5.3 não resolver)
1. **Ouvir o canal 2a99** (Database Change Increment) e registrar no log — ver se
   a balança "avisa" quando há pesagem nova.
2. **Escrever o relógio (2a2b)** antes de pesar (o openScale faz isso; no nosso
   mapa o relógio já vinha certo, mas garantir não custa).
3. Se nada disso resolver, ler o histórico completo e sempre pegar a de **data/hora
   mais recente** — que já é o que o código faz.

## Parte 2 — Envio pro Garmin (FEITO nesta versão)

### Como os outros fazem
- Não existe API oficial grátis do Garmin pra isso (o programa de desenvolvedor
  está fechado). Todo mundo usa o **login não-oficial com e-mail + senha** (o mesmo
  que o `python-garminconnect`/`garth` e o **mi-scale-exporter** usam).
- Esse login tem várias etapas (cookies, token CSRF, OAuth1 → OAuth2) e às vezes
  cai o **2FA** (código de verificação). É chato de fazer do zero.

### O que decidimos usar
A biblioteca **YetAnotherGarminConnectClient** (NuGet, versão 0.0.17, do **mesmo
autor** do mi-scale-exporter). Ela já:
- faz o login com e-mail+senha,
- trata **2FA/código de verificação**,
- guarda os tokens,
- e tem o método **`UploadWeight(...)`** que manda a pesagem de composição corporal.

É **grátis** e roda **direto no celular** (a senha vai do app pro Garmin, sem
servidor no meio). Mantém nossa regra de custo zero.

### O que o app manda pro Garmin hoje
Do que a balança já entrega, mapeamos pro Garmin:
- **peso** → `weight`
- **gordura %** → `percent_fat`
- **água** (convertida de kg pra %) → `percent_hydration`
- **músculo** (convertido de % pra kg) → `muscle_mass`
- **IMC** → `bmi`

### O que ainda NÃO mandamos (porque ainda não decodificamos da balança)
- massa óssea (`bone_mass`)
- gordura visceral (`visceral_fat_rating`)
- idade metabólica (`metabolic_age`)
- BMR/AMR (a biblioteca do Garmin não tem campo direto pra isso no upload de peso)

Esses campos aparecem no app oficial, mas ainda não achamos onde vêm nos bytes
crus da balança. Assim que resolvermos a leitura ao vivo (Parte 1), dá pra
decodificar o resto comparando com o print oficial e completar o envio.

## Referências
- openScale: https://github.com/oliexdev/openScale (PR #753, driver padrão)
- mi-scale-exporter: https://github.com/lswiderski/mi-scale-exporter
- Biblioteca Garmin usada: https://github.com/lswiderski/yet-another-garmin-connect-client
- Campos aceitos pelo Garmin: https://github.com/cyberjunky/python-garminconnect

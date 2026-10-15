# O Coveiro — Guia da Semana 1

**Meta da semana:** dois Coveiros andando juntos, sincronizados em rede, numa cena escura com a câmera isométrica.

---

## 1. Projeto e arquivos (já feito)

O projeto `OCoveiro` (Unity 6.6, URP) já tem os scripts em `Assets/_Project`, o `.gitignore`, o `.gitattributes` e o Input System no modo novo.

## 2. Instalar os pacotes de rede (~5 min)

Até este passo, o Console mostra erros de `Unity.Netcode`: é esperado, eles somem quando o pacote entra.

Em **Window → Package Manager → + → Install package by name**, instale:

| Pacote | Nome para digitar |
| --- | --- |
| Netcode for GameObjects | `com.unity.netcode.gameobjects` |
| Multiplayer Play Mode | `com.unity.multiplayer.playmode` |

Espere compilar. O Console tem que ficar sem erros vermelhos (clique em **Clear** para limpar os antigos).

## 4. Montar tudo com um clique

Na barra de menu aparece **O Coveiro**. Clique em:

**O Coveiro → 3. Criar cena de teste de rede**

Isso cria as pastas, o prefab do jogador (com o Coveiro e a Aprendiz), os materiais e a cena `Assets/_Project/Scenes/TesteRede.unity` já aberta, com o NetworkManager configurado.

## 5. Testar com dois jogadores no mesmo PC

1. Abra **Window → Multiplayer → Multiplayer Play Mode**.
2. Ative o **Player 2**. Uma segunda janela da Unity abre (demora na primeira vez).
3. Aperte **Play**.
4. Na janela principal, clique em **Hospedar (Coveiro)**.
5. Na janela do Player 2, clique em **Entrar (Aprendiz)**, com o IP `127.0.0.1`.
6. Ande com **WASD** em cada janela. Cada um controla só o seu personagem e vê o outro se mexendo.

### Testar entre dois PCs na mesma rede (opcional)

- Gere uma build (**File → Build and Run**).
- No PC que hospeda, descubra o IP local (`ipconfig` no Windows, algo como `192.168.0.15`).
- No outro PC, digite esse IP e clique em **Entrar**. Pode ser necessário liberar a porta **7777** no firewall do Windows.
- Pela internet (casas diferentes) só funciona na semana 3, com o Relay.

## 6. Git (Dev A cria, Dev B clona)

```bash
cd OCoveiro
git init
git lfs install
git add .
git commit -m "Semana 1: projeto base com movimento em rede"
git branch -M main
git remote add origin https://github.com/<usuario>/ocoveiro.git
git push -u origin main
```

O colega instala o Git LFS e faz `git clone`. **Regra de ouro:** nunca editem a mesma cena ao mesmo tempo.

---

## O que cada script faz

Vocês precisam saber explicar isto na apresentação.

| Script | Responsabilidade | Conceito |
| --- | --- | --- |
| `PlayerController` | Lê WASD/analógico e move o personagem relativo à câmera | Só roda no **dono** (`IsOwner`) |
| `PlayerAppearance` | Host vira Coveiro, convidado vira Aprendiz; cor da lanterna | Um prefab, dois papéis |
| `OwnerNetworkTransform` | Sincroniza posição e rotação; quem manda é o dono | Autoridade do cliente para movimento |
| `OwnerNetworkAnimator` | Sincroniza animações (entra com os modelos animados) | Mesma ideia, para o Animator |
| `ConnectionManager` | Solo, hospedar, entrar, sair; limita a sala a 2 jogadores | Aprovação de conexão |
| `ConnectionDebugUI` | Menu provisório para testar | Substituído na semana 3 |
| `IsoCamera` | Câmera isométrica que segue só o jogador local | Cada jogador tem a sua câmera |
| `GameEvents` | Barramento de eventos do jogo | Padrão Observer (desacoplamento) |
| `SetupWizard` (Editor) | Gera pastas, prefab e cena | Automação do editor |

**A ideia central:** existe uma cópia de cada personagem em cada PC. Só a cópia do dono lê o teclado; a outra apenas recebe a posição pela rede. Por isso todo script de gameplay vai perguntar "eu sou o dono?" (`IsOwner`) ou "eu sou o host?" (`IsServer`).

---

## Tarefas do Dev B nesta semana

- [ ] Escolher **uma** família de assets low-poly dark fantasy (KayKit, Quaternius ou similar) e conferir a licença.
- [ ] Escolher os modelos do Coveiro e da Aprendiz e importar em `Assets/ThirdParty`.
- [ ] Baixar do Mixamo: parado (idle), andando e correndo.
- [ ] Montar um Animator Controller com um parâmetro float `Speed` (Idle → Walk quando `Speed > 0.1`).
- [ ] Colocar os modelos dentro de `Visual_Coveiro` e `Visual_Aprendiz` no prefab, no lugar das cápsulas (mantendo os nomes).
- [ ] Ligar o Animator no campo **Animator** do `PlayerController` e adicionar `OwnerNetworkAnimator` no prefab.

## Pronto quando

- [ ] O Console abre sem erros.
- [ ] Host e convidado andam ao mesmo tempo e cada um vê o outro se mexer.
- [ ] O host aparece como Coveiro (escuro) e o convidado como Aprendiz (avermelhada).
- [ ] A câmera segue o jogador certo em cada janela.
- [ ] Um terceiro jogador é recusado com a mensagem de sala cheia.
- [ ] O projeto está no GitHub e o colega conseguiu clonar e rodar.

## Se algo der errado

| Sintoma | Causa provável |
| --- | --- |
| Erros de `UnityEngine.InputSystem` | Pacote Input System não instalado ou Active Input Handling ainda no modo antigo |
| Erros de `Unity.Netcode` | Netcode for GameObjects não instalado |
| Personagem cor-de-rosa | Material sem o shader do URP; confirme que o projeto usa o template Universal 3D |
| O convidado não aparece | O Console mostra o motivo; confira se o host clicou em Hospedar antes |
| Aviso de prefab não registrado na rede | Adicione o `Player.prefab` na lista **Network Prefabs** do NetworkManager |

Qualquer erro: copie a mensagem do Console e mande no chat.

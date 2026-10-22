# O Coveiro — Guia da Semana 3 (parte 1): inimigos

**Meta:** inimigos que avisam antes de bater, perseguem, cambaleiam e morrem, funcionando em rede. E a lanterna passa a ser necessária.

---

## 1. Atualizar o projeto (1 clique)

1. Volte para a Unity e espere compilar (Console sem erros vermelhos).
2. **O Coveiro → 5. Atualizar para a Semana 3 (inimigos)**.

Isso cria, sem apagar nada:

- Dados em `Assets/_Project/Data/Inimigos`: **Errante**, **ErranteVigia**, **Espectro**.
- Prefabs em `Assets/_Project/Prefabs/Inimigos`.
- Na cena `TesteRede`: NavMesh no chão (gerado ao abrir a cena), 2 Errantes, 1 Vigia, 2 Espectros e um nicho com parede ilusória.

## 2. Os inimigos

| Inimigo | Vida (solo / dupla) | Comportamento | Como vencer |
| --- | --- | --- | --- |
| **Errante** | 30 / 45 | Lento. Levanta o braço e fica vermelho por quase 1 s antes de bater | Esquivar do golpe e punir; 3 golpes leves o fazem cambalear |
| **Errante Vigia** | 45 / 67 | Ao ver você, para, pisca em amarelo e grita: acorda os inimigos perto | Matar antes do grito, ou chegar pelo escuro |
| **Espectro** | 20 / 30 | Invisível no escuro, atravessa paredes, tenta vir pelas costas | Acender a lanterna: só na luz ele aparece e leva dano |

Regras gerais: o golpe pesado sempre faz cambalear; inimigo que perde você de vista volta para o posto e recupera a vida; inimigo morto fica no chão (na semana 4, descansar no altar faz todos voltarem).

## 3. Roteiro de teste (Multiplayer Play Mode, Player 2 ligado)

- [ ] Ande para o norte (direção da vela): os Errantes percebem a ~8 m, andam até você e contornam os pilares.
- [ ] O aviso do golpe dá tempo de reagir; esquivar na hora certa evita o dano.
- [ ] Levar golpe: tela treme, personagem é empurrado e trava por um instante.
- [ ] 3 golpes leves seguidos fazem o Errante cambalear e cancelar o golpe que estava preparando.
- [ ] O Vigia grita ("!" em cima dele) e os inimigos por perto vêm junto.
- [ ] Com a lanterna apagada, o Espectro some; golpes passam direto. Acesa, ele aparece e apanha.
- [ ] Fugir para longe: o inimigo desiste, volta ao posto e recupera a vida.
- [ ] Com 2 jogadores, os inimigos têm mais vida (barra acima da cabeça).
- [ ] Inimigos atacam o jogador mais próximo; o caído é ignorado.
- [ ] **Nicho a leste** (com a runa no chão): a parede some quando a lanterna chega perto, e dá para entrar. A runa só aparece na luz.
- [ ] Na janela do convidado, tudo aparece igual: posição, aviso vermelho, queda, Espectro só na luz.

## 4. Ajuste fino

Os números de cada inimigo ficam nos assets em `Data/Inimigos`. Os mais importantes para a sensação:

- `attackWindup`: tempo de aviso. Menor = mais difícil.
- `chaseSpeed` e `detectionRadius`: agressividade.
- `poise`: quantos golpes até cambalear.

---

## Como a IA funciona (para a apresentação)

**Tudo roda no host.** Os clientes só recebem a posição (NetworkTransform) e o estado atual (uma NetworkVariable de 1 byte). Os visuais (aviso vermelho, grito, queda) são calculados em cada PC a partir desse estado.

**Máquina de estados (padrão State):** cada estado é uma classe com `Enter`, `Tick` e `Exit`.

| Estado | Faz | Sai para |
| --- | --- | --- |
| `Idle` | Parado no posto, procura jogadores 4×/s | `Alert` (Vigia) ou `Chase` ao ver alguém |
| `Alert` | Encara e grita; acorda quem estiver perto | `Chase` |
| `Chase` | Persegue; troca para o jogador mais próximo | `Attack` ao chegar perto, `Return` se perder o alvo |
| `Attack` | Aviso → golpe → recuperação | `Chase` |
| `Stagger` | Cambaleia (golpe pesado ou poise quebrado) | `Chase` ou `Return` |
| `Return` | Volta para o posto e recupera a vida | `Idle`, ou `Chase` se vir alguém |
| `Dead` | No chão, sem collider | (semana 4: altar) |

**Movimento (padrão Strategy):** o cérebro só pede "vá até ali"; quem resolve é o motor.

- `NavMeshMotor`: anda pelo NavMesh contornando paredes (Errantes).
- `GhostMotor`: linha reta, atravessa tudo (Espectro).

**Dados (ScriptableObject):** um inimigo novo é só um asset `EnemyData` + um prefab. O código é o mesmo.

**A lanterna como regra do mundo:** `Lantern.IsLit(ponto)` é usado pelo Espectro (aparece e leva dano), pelo bônus de +25% de dano e pelos `SpectralObject` (parede ilusória e runa). Cada PC calcula sozinho a partir das lanternas sincronizadas, então os dois veem a mesma coisa sem mensagens extras.

## Scripts novos

| Script | Responsabilidade |
| --- | --- |
| `EnemyData` | Números de cada tipo de inimigo |
| `EnemyBrain` | Máquina de estados, percepção, ataque, poise, escala do co-op |
| `EnemyState`, `EnemyStates` | Os 7 estados |
| `IEnemyMotor`, `NavMeshMotor`, `GhostMotor` | Movimento |
| `EnemyVisual` | Aviso do golpe, grito, queda, Espectro só na luz (provisório até os modelos) |
| `SpectralObject` | Objetos que aparecem ou somem na luz |
| `RuntimeNavMeshBaker` | Gera o NavMesh ao abrir a cena |

## Próximo: Semana 3 (parte 2)

- Relay da Unity: jogar pela internet com código de entrada, e o menu de verdade.
- Precisa antes: criar o projeto em **cloud.unity.com** e ligar em **Edit → Project Settings → Services**.

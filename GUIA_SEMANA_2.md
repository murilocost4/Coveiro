# O Coveiro — Guia da Semana 2: combate

**Meta da semana:** os dois jogadores batem num boneco de treino, em rede, e bater é *gostoso*.

---

## 1. Atualizar o projeto (1 clique)

1. Volte para a Unity e espere compilar. O Console não pode ter erros vermelhos.
2. Clique em **O Coveiro → 4. Atualizar para a Semana 2 (combate)**.

Isso faz, sem apagar nada do que já existe:

- Cria os dados em `Assets/_Project/Data`: **Pa** (a arma) e **AtributosJogador** (vida, stamina, esquiva, frascos, lanterna).
- Completa o `Player.prefab` com vida, stamina, lanterna, combate, a pá provisória e o flash de dano.
- Coloca 3 bonecos de treino e o HUD provisório na cena `TesteRede`.

## 2. Controles

| Ação | Teclado e mouse | Controle |
| --- | --- | --- |
| Andar | WASD | Analógico esquerdo |
| Mirar o golpe | Mouse | Analógico direito |
| Golpe leve (combo de 3) | Clique esquerdo | X / Quadrado |
| Golpe pesado | Segurar o botão direito e soltar | Y / Triângulo |
| Esquiva | Espaço | B / Bola |
| Lanterna | F | RB / R1 |
| Frasco de cura | Q | LB / L1 |
| **Teste:** levar 25 de dano | K | — |
| **Teste:** recarregar tudo | O | — |

As teclas de teste só existem no editor e em builds de desenvolvimento.

## 3. Roteiro de teste (com Multiplayer Play Mode, Player 2 ligado)

- [ ] Clique repetido faz o combo de 3 golpes; o terceiro é de cima para baixo e empurra mais o boneco.
- [ ] O boneco pisca de branco e balança; o Console mostra `[Treino] ... levou 10 de dano`.
- [ ] Segurar o botão direito levanta a pá; depois de 0,6 s ela "pulsa"; soltar faz a pancada de 30 de dano.
- [ ] Soltar antes de carregar cancela sem gastar stamina.
- [ ] Golpes gastam stamina (barra verde); com a barra zerada não dá para golpear nem esquivar.
- [ ] Espaço rola na direção do WASD. Apertar **K** durante a rolagem não tira vida (i-frames).
- [ ] **K** fora da esquiva: tira 25, a tela treme, o personagem é empurrado e trava por um instante.
- [ ] **Q** com vida faltando: 1 s lento, depois cura 40 e gasta 1 frasco.
- [ ] **K** até zerar: o personagem tomba e levanta sozinho depois de 3 s (provisório, vira "reanimar" na semana 4).
- [ ] **F** acende a lanterna; o óleo desce; com 0 ela apaga. Golpe com o boneco na luz tira 12,5 em vez de 10.
- [ ] **Na outra janela**, o parceiro vê os golpes da pá, a luz da lanterna e o boneco balançando.
- [ ] Os dois batem no mesmo boneco ao mesmo tempo e o dano soma.

## 4. Ajuste fino (Dev B pode fazer sem mexer em código)

Selecione **Pa** ou **AtributosJogador** em `Assets/_Project/Data` e mude os números no Inspector, inclusive com o jogo rodando. Sugestões para testar o "peso":

- `windup` menor = golpe mais responsivo; maior = mais "pesado".
- `lunge` = passo à frente ao golpear.
- `dodgeDistance` e `iFrameDuration` = quão generosa é a esquiva.

Anotem os valores que ficarem bons.

---

## Como o combate funciona (para a apresentação)

**Quem manda em quê:**

| Sistema | Autoridade | Por quê |
| --- | --- | --- |
| Iniciar golpe, esquiva, cura | O dono do personagem | Resposta instantânea, sem atraso de rede |
| O que o golpe acertou e quanto de dano | O host | Os dois PCs precisam concordar; o cliente não escolhe o próprio dano |
| Vida e frascos | O host | Valor único e confiável |
| Stamina e óleo | O dono | Gastos instantâneos; o valor vai para a rede só quando muda 1 ponto |
| i-frames da esquiva | O dono informa, o host respeita | Justiça para o convidado |

**O caminho de um golpe:**

1. O dono aperta o clique → `PlayerCombat` entra no estado `Attacking` e toca a pá na hora.
2. Avisa o parceiro (`AttackVisualRpc`) para ele ver o golpe também.
3. No fim da preparação (`windup`), manda ao host: "golpeei agora, daqui, nesta direção" (`PerformHitRpc`).
4. O host procura alvos num arco à frente, aplica dano com os números **do host**, e +25% se o alvo estiver na luz.
5. `Health` avisa todos os PCs (`DamagedRpc`): flash branco, balanço, tremor de câmera.
6. O host confirma o acerto para quem bateu (`HitConfirmedRpc`): tremor leve e hit-stop na pá.

**Scripts novos:**

| Script | Responsabilidade | Conceito |
| --- | --- | --- |
| `WeaponData`, `PlayerStatsData` | Números do combate | ScriptableObject: dados separados do código |
| `PlayerCombat` | Golpe, combo, carga, esquiva, cura, atordoado, caído | Máquina de estados (State pattern) |
| `PlayerInputReader` | Todo o input do jogador | Responsabilidade única |
| `Health` | Vida genérica (jogador, boneco, inimigos) | Interface `IDamageable`, autoridade do host |
| `IDamageFilter` | Permite recusar dano (i-frames) | Extensão sem alterar o `Health` |
| `PlayerStamina`, `Lantern` | Recursos do jogador | NetworkVariable com escrita do dono |
| `PlayerLife` | Caído e levantar | Provisório até a semana 4 |
| `HitFlash`, `ShovelVisual`, `IsoCamera.Shake` | Feedback | Game feel |
| `TrainingDummy`, `DebugHUD` | Ferramentas de teste | Provisórios |

## Tarefas do Dev B nesta semana

- [ ] Animações do Mixamo: golpe leve (3 variações, se achar), golpe pesado, esquiva (rolamento), beber, levar dano, cair.
- [ ] Primeiro teste de iluminação URP: Volume com vinheta, bloom leve e granulação; ajustar a neblina.
- [ ] Testar o peso do combate e ajustar os números em **Pa** e **AtributosJogador**.

## Pronto quando

- [ ] O roteiro de teste acima passa inteiro, com os dois jogadores.
- [ ] Bater no boneco é gostoso. Se não for, a semana 3 começa por aqui (hit-stop, som, partículas), como manda o GDD.

# Integração dos telefones KIFOME

## Responsabilidades

- `GameManager` continua sendo a fonte de verdade do relógio, saldo, reputação, meta, compras e pagamento.
- `OrderManager` mantém os pedidos, prazos e transições de estado. O prazo começa na ativação e não reinicia ao preparar ou coletar.
- `PlayerController` mantém o pedido em preparação e os IDs das marmitas carregadas. A capacidade continua sendo `InventoryCapacity`.
- `PickupCounter` cria a representação fechada de cada pedido pronto. `MealboxPhysical` guarda o ID e o nome do pedido nessa representação; o conteúdo da preparação permanece no `PlayerController`.
- `KifomePhoneUI` contém a abertura/fechamento, o cabeçalho compartilhado e a navegação. `KifomeCookPhoneUI` controla a lista `Orders`; `KifomeDeliveryPhoneUI` controla a lista `Deliveries` e a tela interna `Delivery Expand`. Os telefones não possuem abas ou telas de upgrades.
- `CookPreparationUI` apresenta o nome da receita, os ingredientes e o estado da preparação. `DeliveryUI` apresenta o nome e os ingredientes de cada marmita carregada, além do contador da mochila.
- `HouseData` fornece nome do cliente, endereço, descrição e imagem para os detalhes do pedido. Os campos novos de endereço e descrição ficam vazios até serem preenchidos nos assets.

## Estrutura dos scripts

```text
Scripts/
  Core/                         GameManager, MatchConfig, MatchSettings
  Players/                      PlayerController, PlayerConfig, SplitScreenSetup
  World/Interactions/            estações, balcão, portas e interações
  Orders/                        OrderManager e dados de pedidos/ingredientes/casas
  Orders/World/                  MealboxPhysical
  UI/MainMenu/                   MainMenuManager
  UI/Phones/Shared/              abertura, navegação e cabeçalho comuns
  UI/Phones/Cook/                KifomeCookPhoneUI e CookPreparationUI
  UI/Phones/Delivery/            KifomeDeliveryPhoneUI e DeliveryUI
  UI/Components/                 cards e indicadores visuais
  Upgrades/                      UpgradeData
```

Os scripts foram movidos com seus arquivos `.meta`; os GUIDs de script existentes foram preservados. Referências serializadas por GUID em cenas e prefabs não precisam ser refeitas apenas por causa da mudança de pasta.

## Configuração da cena e dos prefabs

`UIs.unity` contém as duas hierarquias dos telefones e seus controladores; confira as referências no Inspector ao integrar essas telas à cena de gameplay. Não presuma que referências do Inspector foram preenchidas em outras cenas.

O arquivo `telas.pdf` não está disponível no workspace, então a hierarquia visual não pôde ser conferida contra suas quatro páginas. A configuração abaixo cobre os dados e as referências exigidos pelos fluxos, mas a fidelidade do layout deve ser revisada no Editor quando o PDF estiver disponível.

### 1. Telefone da cozinheira

1. Use um GameObject ativo fora do `phonePanel` com `KifomeCookPhoneUI`.
2. Associe o Canvas, o `GraphicRaycaster`, o painel fechado, os textos compartilhados do cabeçalho e a view `Orders`.
3. Em `KifomeCookPhoneUI`, associe `ordersContent`, `OrderPrefab` em `orderCardPrefab` e, opcionalmente, o texto de lista vazia.
4. No `PlayerController` da cozinheira, associe o telefone em `kifomePhone`; o jogador e a partida são ligados pelo fluxo existente.

### 2. Telefone do entregador

1. Use um GameObject ativo fora do painel com `KifomeDeliveryPhoneUI`.
2. Configure Canvas, raycaster, painel e textos compartilhados do cabeçalho como no telefone da cozinheira.
3. Associe a view `Deliveries`, `deliveryOrdersContent`, `DeliveryPrefab` em `deliveryOrderCardPrefab` e, opcionalmente, o texto de lista vazia.
4. Associe a view separada `Delivery Expand`, o botão de retorno e os textos/imagem de detalhes que existirem no layout. Nome do cliente, endereço, descrição, valor e foto são opcionais; número, prato e tempo são obrigatórios.
5. No `PlayerController` do entregador, associe o telefone em `kifomePhone`; o jogador e a partida são ligados pelo fluxo existente.

Ao abrir o telefone, a view `Deliveries` é exibida. Selecionar um card mostra `Delivery Expand` no lugar da lista; o botão de retorno limpa os detalhes e restaura a lista. Fechar e reabrir o telefone também retorna à lista. A tela do telefone não coleta ou entrega pedidos: as transições continuam dependendo das interações físicas do jogo.

### 3. Mochila e preparação externas

- Adicione `DeliveryUI` a um objeto ativo na interface externa do entregador, fora do painel do telefone. Atribua `deliveryPlayer`, `backpackContent`, um prefab de `TMP_Text` em `mealNamePrefab` e `backpackCountText`. Configure `backpackContent` com `Vertical Layout Group`; cada linha mostra o nome da marmita seguido dos ingredientes e o contador respeita `InventoryCapacity`. Receitas novas usam os nomes de `IngredientData`; receitas legadas resolvem os nomes pelas estações de ingrediente ativas e usam o ID como último recurso.
- Adicione `CookPreparationUI` a um objeto ativo da interface da cozinheira. Atribua `cook`, `recipeNameText`, `ingredientContent`, `CheckListItem.prefab` em `ingredientIndicatorPrefab` e o Renderer da apresentação visual da marmita de preparação. Configure `ingredientContent` com `Vertical Layout Group`. Cada linha usa texto e Toggle não interagível, atualizado pelos ingredientes reais colocados; o texto de status continua opcional.
- Mantenha `PreparationTrashInteractable` em um objeto alcançável pelo raycast do cozinheiro. A lixeira limpa somente o conteúdo de uma preparação ativa.
- Mantenha `PickupCounter` associado ao `OrderManager`, ao `MealboxPhysical.prefab` fechado e ao ponto de exibição existente. O prefab físico representa marmitas prontas no balcão; não é o recipiente aberto da preparação.

### 4. Cards e casas

- `OrderPrefab`: no `KifomeOrderCardUI`, atribua botão, fundo, número, nome, resumo, estado e tempo. A lista da cozinheira usa resumo como ingredientes.
- `DeliveryPrefab`: no `KifomeDeliveryCardUI`, atribua o botão e os cinco textos: nome, número, tempo restante, preço e descrição da casa.
- `CheckListItem.prefab`: atribua o texto do ingrediente, a imagem e o Toggle ao `KifomeOrderListUI`. O Toggle é apenas visual e não aceita marcação manual.
- Abra cada asset `HouseData` usado pelo `MatchConfig` e configure `CustomerName`, `Address`, `Description` e `HouseImage` com os dados reais do projeto. Nenhum endereço ou descrição é gerado pelo código.

### 5. Input, Canvas e split-screen

- Preserve os mapas `PlayerOne`/`PlayerTwo` e os schemes configurados no `PlayerInput`. O projeto contém `TogglePhone` (Tab em teclado e botão norte do gamepad), `Confirm`, `Cancel` e `Move`; o telefone não cria um segundo sistema de input.
- Mantenha um único `EventSystem` com `InputSystemUIInputModule`. O telefone suspende a navegação global padrão enquanto aberto e recebe a navegação do mapa do seu próprio jogador.
- Configure cada Canvas para a câmera do dono e para a viewport split-screen correspondente. O script atribui `phoneCanvas.worldCamera` à câmera do jogador; isso não substitui o ajuste do modo de renderização, anchors e layout no Editor.
- O componente controlador deve permanecer ativo ao fechar. Apenas `phonePanel` é ocultado. O cursor é solicitado em conjunto pelas instâncias abertas que usam mouse/teclado e retorna ao estado anterior quando nenhuma delas precisa dele.
- Use `KifomeCookPhoneUI` ou `KifomeDeliveryPhoneUI` como componente dos telefones; `KifomePhoneUI` é somente a classe-base compartilhada. A cena `UIs.unity` já associa as views e os prefabs atuais e desativa as views/botões antigos de abas. Para outras cenas, configure manualmente os campos listados acima.

## Verificação em Play Mode

- [ ] Cada jogador abre/fecha somente seu telefone com o binding correspondente; gameplay e raycast de interação ficam bloqueados enquanto aberto.
- [ ] Hora, saldo, reputação e meta aparecem iguais nos dois telefones.
- [ ] O telefone da cozinheira lista somente pedidos disponíveis/em preparação; a seleção acompanha a preparação real e não substitui uma preparação ativa.
- [ ] Receita exata conclui automaticamente uma única vez; ingredientes repetidos são permitidos e validados por quantidade; ingredientes incorretos não concluem.
- [ ] A lixeira limpa o conteúdo sem duplicar pedidos; a marmita fechada aparece uma vez no balcão com o ID correto.
- [ ] O telefone do entregador lista pedidos no balcão e na mochila; clicar abre detalhes e voltar restaura a lista.
- [ ] O tempo exibido permanece ligado ao prazo original do pedido. Endereço, descrição e foto correspondem ao `HouseData` configurado.
- [ ] A coleta funciona somente para pedidos prontos e com espaço. A mochila externa atualiza nome, ingredientes e contador até a capacidade real; uma coleta excedente não muda o pedido.
- [ ] O botão do detalhe seleciona somente pedido na mochila; casa errada não remove marmita nem altera estado; a porta correta conclui uma única vez e atualiza saldo/meta conforme as regras existentes.
- [ ] Teste os dois telefones simultaneamente com cursor, teclado e gamepad e confirme que desativar um não interfere no outro.

O build C# pode verificar compilação, mas a associação de objetos, o layout visual e o fluxo físico ainda precisam ser confirmados na cena em Play Mode.

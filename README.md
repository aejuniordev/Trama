# Trama — editor de hachuras .PAT

Ferramenta web em C# (Blazor + .NET 10) para desenhar padrões de hachura, gerar o arquivo `.pat` para Revit ou AutoCAD e salvar os projetos num banco SQLite. Inspirada no pattycake.io.

## Como rodar

Requisito: [SDK do .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
cd Trama
dotnet run
```

Abra o endereço mostrado no terminal (por exemplo `http://localhost:5000`). Na primeira execução o arquivo `trama.db` é criado na pasta do projeto, com a tabela `Projects`.

Para outro local ou nome de banco, altere `ConnectionStrings:Trama` em `appsettings.json`.

## Usando

Desenhe dentro do quadro de repetição. Cada linha vira uma família de linhas no `.pat`, que se repete em todos os quadros vizinhos.

| Ferramenta ou opção | O que faz |
|---|---|
| Linha (L) | Arraste, ou clique no início e no fim. Shift trava na horizontal/vertical. |
| Selecionar (V) | Clique numa linha; arraste as pontas ou a linha inteira; edite x₁ y₁ x₂ y₂ no rodapé. |
| Apagar (E) | Clique ou arraste sobre as linhas. |
| Correção automática | Ao soltar, ajusta o ângulo para a direção "limpa" mais próxima (até 1°), para a linha se repetir sem arredondamento. |
| Orto | Só horizontal e vertical. |
| Grade | Encaixa nos cruzamentos da grade. |
| Pontas | Encaixa nas pontas e nos pontos médios das outras linhas. |

Atalhos: Ctrl+S salva, Ctrl+Z / Ctrl+Y desfaz e refaz, Delete apaga a linha selecionada, Esc cancela.

O painel **Arquivo .PAT** mostra o texto com marcas de status por linha (clique numa linha do texto para selecioná-la no desenho). **Editar texto** transforma o resultado em texto livre para ajustes manuais; **Importar .pat** abre arquivos existentes nesse mesmo modo. A **Pré-visualização** desenha o padrão a partir do próprio texto, como o Revit/AutoCAD fariam.

## Como a linha vira .PAT

Os quadros repetidos formam uma rede gerada por (W, 0) e (0, H). Uma linha só se repete sem emendas se a sua direção for **u = (I·W, J·H)**, com I e J inteiros primos entre si. A partir disso:

- `angle` = atan2(J·H, I·W)
- `offset` = W·H / |u| (distância entre linhas paralelas vizinhas)
- `shift` = projeção de v = (p·W, q·H) sobre u, com I·q − J·p = 1 (Euclides estendido)
- Traços: se a linha for mais curta que |u|, `dash = comprimento`, `space = −(|u| − comprimento)`.

Exemplo do quadro 12×12 com a linha de (0,12) a (12,0): I = 1, J = −1, e o resultado é `315, 0, 12, 8.485281374, 8.485281374`, igual ao Pattycake.

Detalhes que o gerador trata:

- Números sempre com ponto decimal, mesmo com o Windows em português.
- Revit: cabeçalhos `;%UNITS`, `;%VERSION=3.0` e `;%TYPE=MODEL|DRAFTING`.
- AutoCAD: nome sem espaços, valores compactos para caber no limite de 80 caracteres por linha, linha em branco no final. Para milímetros, use o padrão em `acadiso.pat` (MEASUREMENT=1).
- Acentos são removidos do nome e da descrição dentro do arquivo (o nome original fica salvo no banco).
- Avisos para ângulos arredondados e para linhas "densas", que só se repetem depois de muitos quadros e que o Revit pode recusar. O limite é o campo **Complexidade máx.**

## Estrutura

```
Trama/
├── Program.cs                     Inicialização (Blazor interativo no servidor + SQLite)
├── appsettings.json               String de conexão do banco
├── Models/                        Vec, Segment, PatternDocument, PatLine
├── Services/
│   ├── HatchMath.cs               Matemática da rede (direção, shift, offset, correção)
│   ├── PatGenerator.cs            Desenho → texto .PAT com avisos
│   ├── PatParser.cs               Texto .PAT → linhas, com validação
│   ├── PatRenderer.cs             Linhas .PAT → caminho SVG da pré-visualização
│   ├── PatFormat.cs               Formatação numérica e de nomes
│   ├── PatternTemplates.cs        Modelos prontos
│   └── EditorState.cs             Estado do editor, desfazer/refazer
├── Data/                          EF Core: Project, AppDbContext, ProjectRepository
├── Components/
│   ├── Pages/Home.razor           Tela principal, salvar/abrir/importar, atalhos
│   └── Studio/                    Quadro, texto .PAT, pré-visualização, barra lateral
└── wwwroot/                       app.css, js/trama.js (ponteiro, teclado, download)
```

## Sobre "C# no navegador"

O projeto usa Blazor com renderização interativa no servidor: toda a lógica é C#, a interface roda no navegador e as ações chegam ao servidor por uma conexão SignalR. Isso deixa o SQLite num arquivo único do lado do servidor, compartilhado por quem acessar a aplicação. O JavaScript se limita a converter a posição do mouse para coordenadas do desenho, atalhos de teclado, cópia e download.

Se preferir que o C# rode inteiramente dentro do navegador (Blazor WebAssembly), `Models/` e `Services/` funcionam sem alteração; seria preciso trocar a camada `Data/` por uma API ou por armazenamento local no navegador.

## O que foi testado

- Verificação por força bruta: em quadros 12×12, 12×8, 300×150 e 7,5×3, todas as cópias de 1.587 linhas aleatórias caem exatamente sobre os traços gerados, e os espaços ficam vazios.
- Interação simulada nos componentes (servidor em cultura pt-BR): desenhar arrastando e com dois cliques, Shift, correção automática, avisos, desfazer/refazer, arrastar pontas e linha inteira, borracha, troca de unidade e de tamanho do quadro, modelos, AutoCAD, modo texto com erros, ida e volta pelo modelo do banco e importação.

A camada `Data/` com EF Core não foi compilada no ambiente onde o projeto foi gerado, porque o acesso ao NuGet estava bloqueado; o restante foi compilado e testado com um repositório em memória de mesma interface. O `dotnet run` baixa o pacote `Microsoft.EntityFrameworkCore.Sqlite` normalmente.

## Limitações

- Só linhas retas (sem arcos).
- A importação abre o arquivo como texto; ela não reconstrói o desenho a partir das linhas.
- A página de documentação do pattycake.io só carrega com JavaScript e não pôde ser lida; o formato segue a especificação padrão de `.pat` do AutoCAD/Revit, conferida com o exemplo do print.

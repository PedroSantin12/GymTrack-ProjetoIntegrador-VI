# GymTrack

Aplicativo mobile acadêmico para organizar treinos de musculação, registrar sessões e acompanhar evolução. O projeto usa .NET MAUI, C#, XAML, MVVM e SQLite, com Android como plataforma principal.

## Abrir no Visual Studio Community

1. Confirme que a carga de trabalho **Desenvolvimento de interface do usuário de aplicativo multiplataforma .NET** e o SDK Android estão instalados.
2. Execute `Abrir-GymTrack.cmd`.
3. No Visual Studio, selecione um emulador Android e execute o projeto `GymTrack`.

O atalho cria a unidade temporária `M:` e abre a solução por um caminho somente ASCII. Isso é necessário porque as ferramentas Android rejeitam o caractere acentuado presente no caminho original deste workspace.

Para compilar no terminal:

```powershell
./Abrir-GymTrack.cmd
dotnet build M:\GymTrack\GymTrack.csproj -f net10.0-android
dotnet test M:\GymTrack.Tests\GymTrack.Tests.csproj
```

## Estrutura e funcionalidades implementadas

- `MainFlyoutPage` é a raiz do aplicativo.
- `MainTabbedPage` contém quatro abas, cada uma hospedada em sua própria `NavigationPage`: Início, Treinos, Histórico e Evolução.
- O menu lateral fornece acesso a Exercícios e Sobre.
- O CRUD de exercícios usa navegação modal real com `PushModalAsync` e `PopModalAsync`.
- A tela de exercícios possui busca em tempo real com debounce, filtro por grupo muscular e opção de exibir arquivados.
- O formulário valida os campos, evita duplicatas e persiste o estado ativo definido pelo `CheckBox`.
- Exclusões exigem confirmação; exercícios vinculados são arquivados para preservar o histórico.
- A tela de treinos lista quantidade de exercícios, última execução e mantém a ação Iniciar visível.
- A lista de treinos também permite busca por nome em tempo real, sem diferenciar acentos e maiúsculas.
- O editor de treino permite rascunhos, seleção pesquisável de exercícios ativos, séries, repetições, carga opcional, remoção e reordenação.
- A composição completa do treino é salva em uma transação e reaberta preservando ordem e valores planejados.
- Treinos sem exercícios não podem iniciar; treinos com histórico não podem ser excluídos.
- A execução do treino abre em tela modal focada, com cronômetro, progresso, volume e valores planejados pré-preenchidos.
- Cada série usa edição direta de carga/repetições, conclusão persistida, feedback Toast e opção de adicionar/remover série não concluída.
- Sessões em andamento podem ser retomadas; sair exige confirmação e finalizar grava `FinishedAt` após confirmação e resumo.
- O Histórico usa `ListView` explicitamente, ordena sessões da mais recente para a mais antiga e filtra por treino, período predefinido ou intervalo de datas.
- O detalhe da sessão apresenta exercícios, séries, duração e volume reais, com atalho para Evolução.
- A tela Evolução usa `RadioButton` exclusivo para alternar entre maior carga e volume e renderiza dados persistidos com LiveCharts.
- O Dashboard permite iniciar um treino, mostra o último treino, resumo semanal e sessões recentes.
- Operações concluídas e falhas recuperáveis usam feedback não bloqueante com Toast.
- Cores, espaçamentos, estilos, ícone e splash do GymTrack ficam centralizados em `Resources`.
- A persistência possui cinco tabelas SQLite, inicialização assíncrona, índices, chaves estrangeiras e DAOs explícitos.

As fases 0 a 9 da especificação estão implementadas. O fluxo principal foi validado anteriormente em emulador Android. Após as correções desta revisão, a versão atual passou nos testes automatizados e na compilação Android; ainda falta uma nova execução no emulador.

## Roteiro rápido de demonstração

1. Abra o Dashboard e inicie um treino cadastrado.
2. Conclua ao menos uma série e finalize a sessão.
3. Abra o Histórico, aplique os filtros e toque na sessão para ver os detalhes.
4. Use **Ver evolução** e alterne entre **Maior carga** e **Volume**.
5. Pelo menu lateral, abra Exercícios e Sobre.

Para o gráfico demonstrar variação temporal, registre o mesmo exercício em pelo menos duas sessões concluídas.

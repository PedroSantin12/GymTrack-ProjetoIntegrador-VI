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
- Operações concluídas e falhas recuperáveis usam feedback não bloqueante com Toast.
- Cores, espaçamentos, estilos, ícone e splash do GymTrack ficam centralizados em `Resources`.
- A persistência possui cinco tabelas SQLite, inicialização assíncrona, índices, chaves estrangeiras e DAOs explícitos.

As fases 0 a 3 da especificação estão implementadas. As próximas funcionalidades serão adicionadas na ordem definida pelo documento-mestre.

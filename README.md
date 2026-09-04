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
dotnet build M:\GymTrack.sln -f net10.0-android
```

## Estrutura inicial

- `MainFlyoutPage` é a raiz do aplicativo.
- `MainTabbedPage` contém quatro abas, cada uma hospedada em sua própria `NavigationPage`: Início, Treinos, Histórico e Evolução.
- O menu lateral fornece acesso a Exercícios e Sobre.
- O cadastro de exercício já demonstra navegação modal real com `PushModalAsync` e `PopModalAsync`.
- Cores, espaçamentos, estilos, ícone e splash do GymTrack ficam centralizados em `Resources`.

As funcionalidades de persistência e CRUD são implementadas incrementalmente conforme as fases da especificação do projeto.

# GymTrack

Aplicativo acadêmico para organizar treinos de musculação, registrar sessões e acompanhar a evolução. Usa .NET 10, .NET MAUI, C#, XAML, MVVM e SQLite, com Android como plataforma principal.

O aplicativo começa no login e abre a área correspondente à conta: funcionário ou consumidor. A entrada e o cadastro usam fotos de treino, o nome GymTrack e formulários sem rolagem. A interface usa fundo escuro, tons de cinza, destaque laranja `#FF913A` e navegação inferior arredondada.

## Acesso de demonstração

As contas abaixo são criadas na primeira inicialização:

| Perfil | E-mail | Senha |
| --- | --- | --- |
| Consumidor | `consumidor@gymtrack.app` | `GymTrack123!` |
| Funcionário | `funcionario@gymtrack.app` | `GymTrack123!` |

Na tela de login, informe o e-mail e a senha e toque em **Entrar**. O direcionamento depende do perfil da conta autenticada. Para alternar de conta, use **Perfil → Sair da conta**.

Para criar uma conta pessoal, toque em **Cadastrar-se**, informe nome, e-mail e senha e confirme a senha. Novas contas recebem o perfil de consumidor e entram automaticamente após o cadastro. Contas de funcionário são gerenciadas pela equipe; o cadastro público não concede esse perfil.

As contas, os exercícios, os treinos e as sessões persistem localmente no SQLite do dispositivo. Este acesso é demonstrativo: ainda não há servidor de autenticação, backend na nuvem ou sincronização entre dispositivos. As credenciais acima não devem ser usadas em produção. Alterações nas contas de demonstração são preservadas nas próximas inicializações.

## Áreas do aplicativo

**Funcionário**

- Painel com resumo de usuários e treinos prontos.
- Usuários: visualizar, pesquisar, editar e desativar contas.
- Exercícios: cadastrar nome, grupo muscular e descrição, pesquisar e gerenciar exercícios ativos ou arquivados.
- Treinos prontos: criar, editar e excluir rotinas com exercícios, séries, repetições e carga planejada.
- Estatísticas: consultar quantidade de usuários, contas ativas e desativadas, perfis de acesso, total de treinos criados e treinos prontos.
- Perfil com os dados da conta e opção de sair.

**Consumidor**

- Navegação inferior flutuante com **Treinos**, **Evolução** e **Perfil**.
- Treinos é a primeira página após o login, com opções para criar uma rotina ou explorar os treinos prontos.
- Biblioteca de treinos prontos com pesquisa, detalhe dos exercícios, demonstração de execução e cópia para a conta.
- Meus treinos com criação, edição e início de sessão.
- Execução com cronômetro, registro de séries, repetições e carga, retomada de sessão e finalização.
- Evolução com sessões concluídas, tempo acumulado, frequência nos últimos sete dias e treinos recentes do próprio usuário.
- Perfil com os dados da conta e opção de sair.

O catálogo inicial inclui 33 exercícios em 20 grupos musculares e três treinos prontos de peito, pernas e glúteos. Ao abrir um treino pronto, o consumidor pode examinar a sequência e a execução de cada exercício antes de adicioná-lo à própria conta. Ele recebe uma cópia independente: alterar ou excluir o modelo do catálogo não modifica a cópia. O acesso a treinos pessoais e sessões é restrito ao dono da conta; o catálogo de exercícios é compartilhado.

## Roteiro de demonstração

1. Entre como **Funcionário** e abra **Painel → Exercícios** para cadastrar os movimentos.
2. Abra **Treinos**, crie um treino pronto, adicione exercícios e salve sua composição.
3. Saia da conta pelo **Perfil** e entre como **Consumidor**.
4. Em **Treinos**, selecione **Explorar treinos prontos**, abra a rotina, confira os exercícios e toque em **Adicionar aos meus treinos** para revisar sua cópia no editor.
5. De volta a **Treinos**, toque em **Começar treino**, registre as séries e finalize a sessão.
6. Consulte **Evolução** para ver os dados dessa conta. Também é possível criar uma rotina própria por **+ Criar treino**.

Treinos sem exercícios não podem iniciar. Ao atualizar uma instalação antiga, os treinos existentes sem proprietário passam a ser modelos do catálogo. Seus registros são preservados; o histórico antigo não é atribuído automaticamente a uma das novas contas.

## Abrir no Visual Studio Community

1. Confirme que a carga de trabalho **Desenvolvimento de interface do usuário de aplicativo multiplataforma .NET** e o SDK Android estão instalados.
2. Se já tinha uma cópia clonada, execute **Git → Pull** no Visual Studio para obter a versão mais recente da `main`.
3. Feche a solução e execute `Abrir-GymTrack.cmd` na pasta do clone que deseja abrir.
4. No Visual Studio, selecione um emulador Android e execute o projeto `GymTrack`.

O atalho cria uma unidade temporária e abre a solução por um caminho somente ASCII. Isso é necessário porque as ferramentas Android rejeitam o caractere acentuado presente no caminho original deste workspace.

A primeira tela da versão reformulada é o login. Se aparecer a interface antiga, confira se `GymTrack/Views/LoginPage.xaml` existe na cópia aberta e se `GymTrack/App.xaml.cs` inicia `LoginPage`. **Windows Machine** abre o aplicativo no Windows; para conferir a experiência Android, selecione um emulador ou celular Android na barra superior.

Para compilar no terminal:

```powershell
./Abrir-GymTrack.cmd
dotnet build M:\GymTrack\GymTrack.csproj -f net10.0-android
dotnet test M:\GymTrack.Tests\GymTrack.Tests.csproj
```

## Desenvolvimento no ambiente de nuvem

O ambiente configurado disponibiliza .NET, MAUI, JDK e SDK Android. Em um novo terminal:

```bash
source /workspace/.gymtrack-setup/env.sh
cd /workspace/GymTrack-ProjetoIntegrador-VI
dotnet test GymTrack.Tests/GymTrack.Tests.csproj
dotnet build GymTrack/GymTrack.csproj -f net10.0-android -p:AppSettingsDirectory=/workspace/.gymtrack-setup/xamarin
```

Os testes automatizados e a compilação Android validam a lógica e a geração do aplicativo. A verificação visual e a interação com as telas exigem um dispositivo ou emulador Android e devem ser feitas separadamente.

## Organização do código

- `Views/LoginPage` e `ViewModels/LoginViewModel`: entrada e autenticação local.
- `Views/Admin`: painel, usuários, treinos prontos e estatísticas do funcionário.
- `Views/Consumer`: treinos pessoais, biblioteca, evolução e perfil.
- `Views/Components`: componentes visuais e navegação inferior compartilhados.
- `Views/WorkoutEditPage`, `Views/WorkoutSessionPage` e seus view models: composição e execução de treinos.
- `Services`: contas, sessão atual, permissões, navegação e catálogo de treinos.
- `Data` e `Models`: banco SQLite, migração dos treinos existentes, DAOs e entidades.
- `Resources`: cores, estilos e demais recursos visuais.
- `GymTrack.Tests`: testes de persistência, serviços, permissões e view models.

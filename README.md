# GymTrack

Aplicativo acadêmico para organizar treinos de musculação, registrar sessões e acompanhar a evolução. Usa .NET 10, .NET MAUI, C#, XAML, MVVM e SQLite, com Android como plataforma principal.

O aplicativo começa no login e abre a área correspondente à conta: funcionário ou consumidor. A entrada e o cadastro usam fotos de treino, o símbolo GymTrack e formulários que se ajustam à altura disponível. A interface usa fundo escuro, tons de cinza, destaque laranja `#FF913A` e navegação inferior fixa.

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

- **Início** mostra os totais de alunos ativos e treinos no catálogo, além de atalhos.
- **Alunos** permite pesquisar contas ativas de consumidores e abrir a ficha individual com nome e e-mail.
- **Treinos** permite criar, editar e excluir rotinas com exercícios, séries, repetições e carga planejada.
- **Exercícios** reúne cadastro, edição, busca e filtros por grupo muscular, com categorias amplas como Costas, Pernas e Braços.
- **Perfil** reúne os dados da conta, o gerenciamento de contas e as estatísticas administrativas. Os treinos pessoais e a evolução exigem uma conta de consumidor.

**Consumidor**

- Navegação inferior fixa com **Treinos**, **Evolução** e **Perfil**.
- Treinos é a primeira página após o login e reúne resumo de sessões reais, retomada do treino em andamento e rotinas pessoais.
- Biblioteca com seis modelos de base (Full Body, Upper Body, Lower Body, Push, Pull e Legs) e treinos prontos de peito, pernas e glúteos. É possível pesquisar e conferir exercícios, séries, repetições e guias de execução antes de copiar o treino para a conta.
- Meus treinos com criação, edição, duplicação, exclusão confirmada e início de sessão. Ao excluir uma rotina com sessões concluídas, ela sai da lista e o histórico permanece disponível.
- Execução com imagens demonstrativas dos exercícios, cronômetro, registro de séries, repetições e carga, retomada de sessão e finalização.
- Evolução com registro manual, histórico, frequência, tempo e carga. O **Calendário** marca os dias com registros manuais e sessões concluídas, agrupando treinos do mesmo dia.
- Perfil com dados da conta, peso e medidas corporais, gráfico de peso e opção de sair.

O catálogo inicial inclui 33 exercícios em 20 grupos musculares e três treinos prontos de peito, pernas e glúteos, além dos seis modelos de base. Ao abrir qualquer treino pronto, o consumidor pode examinar a sequência, séries, repetições e o guia de cada exercício antes de adicioná-lo à própria conta. Todos os 33 exercícios iniciais têm imagens anatômicas estáticas incluídas no aplicativo e funcionam sem internet. A origem e a licença das imagens estão na seção [Créditos das imagens](#créditos-das-imagens). Exercícios personalizados sem imagem correspondente exibem uma indicação discreta de mídia indisponível. O consumidor recebe uma cópia independente: alterar ou excluir um treino pronto do catálogo não modifica a cópia. O acesso a treinos pessoais e sessões é restrito ao dono da conta; o catálogo de exercícios é compartilhado.

Na conta de demonstração do consumidor, a aba **Evolução** inclui alguns registros manuais marcados como **Exemplo**. Eles aparecem no histórico, mas não entram nas estatísticas; podem ser excluídos e não reaparecem. Registros manuais e medidas corporais ficam apenas no banco local do dispositivo e são separados por conta. Volume e carga são calculados somente a partir de séries realmente registradas no app.

## Roteiro de demonstração

1. Entre como **Funcionário**. Use **Exercícios** para cadastrar movimentos e **Treinos** para criar uma rotina com pelo menos um exercício.
2. Abra **Alunos** e selecione **Abrir ficha** para consultar os dados de uma conta ativa.
3. Entre como **Consumidor**, abra **Treinos** e use **Explorar treinos prontos** para copiar um modelo para a conta.
4. Toque em **Iniciar treino**, registre as séries e finalize a sessão. Se sair antes, use **Continuar treino** para retomá-la.
5. Em **Evolução**, registre também um treino feito em outra data e abra **Calendário** para conferir os dias marcados.

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
```

## Desenvolvimento no ambiente de nuvem

O ambiente configurado disponibiliza .NET, MAUI, JDK e SDK Android. Em um novo terminal:

```bash
source /workspace/.gymtrack-setup/env.sh
cd /workspace/GymTrack-ProjetoIntegrador-VI
dotnet build GymTrack/GymTrack.csproj -f net10.0-android -p:AppSettingsDirectory=/workspace/.gymtrack-setup/xamarin
```

Para conferir a interface e testar o fluxo de demonstração no Android, execute o aplicativo em um dispositivo ou emulador.

## Organização do código

- `Views/LoginPage` e `ViewModels/LoginViewModel`: entrada e autenticação local.
- `Views/Admin`: início, alunos e fichas, gerenciamento de contas, treinos prontos e estatísticas do funcionário.
- `Views/Consumer`: treinos pessoais, biblioteca, evolução e perfil.
- `Views/Components`: componentes visuais e navegação inferior compartilhados.
- `Views/WorkoutEditPage`, `Views/WorkoutSessionPage` e seus view models: composição e execução de treinos.
- `Services`: contas, sessão atual, permissões, navegação, catálogo e consulta de alunos.
- `Data` e `Models`: banco SQLite, migração dos treinos existentes, DAOs e entidades.
- `Resources`: cores, estilos e demais recursos visuais.

## Créditos das imagens

A biblioteca inicial inclui 32 ilustrações anatômicas geradas para o GymTrack e uma imagem de referência licenciada.

<details>
<summary>Ilustrações geradas para o GymTrack</summary>

Prompt base usado: “Educational exercise anatomy plate, white landscape canvas, two adjacent views of the same adult athlete in the start and finish positions, grayscale realistic musculature, restrained muted red highlight on the working muscles, accurate equipment and posture, no labels, arrows, logos or watermark.” Cada chamada acrescentou o nome do exercício, equipamento, posições e músculos específicos. Em casos de bloqueio automático, o atleta foi representado com camiseta e calça esportivas opacas. A flexão de punhos foi refeita com os antebraços apoiados e movimento exclusivo dos punhos.

| Exercício | Arquivo |
| --- | --- |
| Prancha frontal | `anatomy_prancha_frontal.png` |
| Abdominal no solo | `anatomy_abdominal_no_solo.png` |
| Flexão de punhos com halteres | `anatomy_flexao_de_punhos_com_halteres.png` |
| Rosca direta com halteres | `anatomy_rosca_direta_com_halteres.png` |
| Rosca martelo | `anatomy_rosca_martelo.png` |
| Remada alta com halteres | `anatomy_remada_alta_com_halteres.png` |
| Puxada frontal | `anatomy_puxada_frontal.png` |
| Remada curvada | `anatomy_remada_curvada.png` |
| Extensão lombar no banco | `anatomy_extensao_lombar_no_banco.png` |
| Desenvolvimento com halteres | `anatomy_desenvolvimento_com_halteres.png` |
| Elevação lateral | `anatomy_elevacao_lateral.png` |
| Supino reto com halteres | `anatomy_supino_reto_com_halteres.png` |
| Supino inclinado com halteres | `anatomy_supino_inclinado_com_halteres.png` |
| Crucifixo com halteres | `anatomy_crucifixo_com_halteres.png` |
| Flexão de braços | `anatomy_flexao_de_bracos.png` |
| Mobilidade cervical | `anatomy_mobilidade_cervical.png` |
| Encolhimento com halteres | `anatomy_encolhimento_com_halteres.png` |
| Tríceps na polia | `anatomy_triceps_na_polia.png` |
| Tríceps francês | `anatomy_triceps_frances.png` |
| Cadeira abdutora | `anatomy_cadeira_abdutora.png` |
| Cadeira adutora | `anatomy_cadeira_adutora.png` |
| Elevação pélvica | `anatomy_elevacao_pelvica.png` |
| Agachamento sumô | `anatomy_agachamento_sumo.png` |
| Coice no cabo | `anatomy_coice_no_cabo.png` |
| Elevação de panturrilhas em pé | `anatomy_elevacao_de_panturrilhas_em_pe.png` |
| Mesa flexora | `anatomy_mesa_flexora.png` |
| Levantamento terra romeno | `anatomy_levantamento_terra_romeno.png` |
| Agachamento livre | `anatomy_agachamento_livre.png` |
| Leg press | `anatomy_leg_press.png` |
| Caminhada na esteira | `anatomy_caminhada_na_esteira.png` |
| Polichinelo | `anatomy_polichinelo.png` |
| Alongamento geral | `anatomy_alongamento_geral.png` |

</details>

### Imagem licenciada

| Exercício | Arquivo | Autor / fonte | Licença |
| --- | --- | --- | --- |
| Cadeira extensora | `exercise_leg_extension.jpg` | [Franpol / wger](https://wger.de/api/v2/exerciseimage/386/) | [CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/) |

Os arquivos ficam em `GymTrack/Resources/Images`; o vínculo entre exercício e arquivo está em `GymTrack/Services/ExerciseMediaCatalog.cs`. Exercícios personalizados sem imagem própria continuam sem demonstração atribuída.

Ideias futuras:

Um tipo de premiação dentro do aplicativo, que funcione por metas.

O usuário cadastrar fotos para si mesmo, para auxilio na evolução.

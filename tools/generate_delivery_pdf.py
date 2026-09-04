from __future__ import annotations

from pathlib import Path

from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import mm
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import (
    Image,
    KeepTogether,
    PageBreak,
    Paragraph,
    SimpleDocTemplate,
    Spacer,
    Table,
    TableStyle,
)


ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "Entrega" / "GymTrack_Relatorio_Final.pdf"
ARTIFACTS = ROOT / "artifacts" / "android"


SCREENS = [
    (
        "Início / Dashboard",
        ARTIFACTS / "final" / "dashboard.png",
        "Apresenta a identidade GymTrack, seleção e início rápido de treino, último treino, resumo semanal e sessões recentes.",
    ),
    (
        "Menu lateral",
        ARTIFACTS / "final" / "menu.png",
        "Demonstra o FlyoutPage e concentra os acessos secundários para Exercícios e Sobre sem ocupar uma aba principal.",
    ),
    (
        "Treinos",
        ARTIFACTS / "final" / "workouts.png",
        "Lista os treinos persistidos, quantidade de exercícios, última execução e ação direta para iniciar uma sessão.",
    ),
    (
        "Editar treino",
        ARTIFACTS / "final" / "workout-edit.png",
        "Permite editar nome, descrição, ordem dos exercícios, séries planejadas, repetições e carga opcional.",
    ),
    (
        "Selecionar exercício",
        ARTIFACTS / "final" / "exercise-picker.png",
        "Seletor modal pesquisável usado na composição do treino, com atalho para gerenciar o catálogo de exercícios.",
    ),
    (
        "Exercícios",
        ARTIFACTS / "final" / "exercises.png",
        "Exibe busca em tempo real, filtro por grupo muscular, opção de mostrar arquivados e ações de edição ou arquivamento.",
    ),
    (
        "Criar exercício",
        ARTIFACTS / "final" / "exercise-edit.png",
        "Formulário modal com nome, grupo muscular, observações e CheckBox de estado ativo, incluindo validações antes de salvar.",
    ),
    (
        "Sessão de treino",
        ARTIFACTS / "phase5" / "session-progress.png",
        "Tela focada de execução com cronômetro, progresso, volume, desempenho anterior e linhas de séries em Grid.",
    ),
    (
        "Histórico",
        ARTIFACTS / "final" / "history.png",
        "Usa ListView explicitamente e mostra sessões em ordem decrescente, com treino, data, duração, séries e volume, além de filtros.",
    ),
    (
        "Detalhe da sessão",
        ARTIFACTS / "final" / "history-detail.png",
        "Detalha os exercícios e séries executadas, duração e volume calculado, com navegação para a evolução.",
    ),
    (
        "Evolução",
        ARTIFACTS / "final" / "progress-max-load-fixed2.png",
        "Gráfico temporal com dados reais do SQLite. RadioButtons exclusivos alternam imediatamente entre maior carga e volume.",
    ),
    (
        "Sobre",
        ARTIFACTS / "final" / "about.png",
        "Identifica o projeto acadêmico, seus autores e as principais tecnologias empregadas.",
    ),
]


def add_page_number(canvas, document):
    canvas.saveState()
    canvas.setFont("GymTrack", 8)
    canvas.setFillColor(colors.HexColor("#5D6973"))
    canvas.drawRightString(A4[0] - 16 * mm, 10 * mm, f"GymTrack  •  {document.page}")
    canvas.restoreState()


def scaled_image(path: Path, max_width: float, max_height: float) -> Image:
    image = Image(str(path))
    scale = min(max_width / image.imageWidth, max_height / image.imageHeight)
    image.drawWidth = image.imageWidth * scale
    image.drawHeight = image.imageHeight * scale
    return image


def main() -> None:
    missing = [str(path) for _, path, _ in SCREENS if not path.exists()]
    if missing:
        raise FileNotFoundError("Capturas ausentes:\n" + "\n".join(missing))

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    pdfmetrics.registerFont(TTFont("GymTrack", r"C:\Windows\Fonts\arial.ttf"))
    pdfmetrics.registerFont(TTFont("GymTrackBold", r"C:\Windows\Fonts\arialbd.ttf"))

    styles = getSampleStyleSheet()
    title = ParagraphStyle(
        "Title",
        parent=styles["Title"],
        fontName="GymTrackBold",
        fontSize=28,
        leading=33,
        textColor=colors.HexColor("#111820"),
        alignment=TA_CENTER,
        spaceAfter=12,
    )
    subtitle = ParagraphStyle(
        "Subtitle",
        parent=styles["BodyText"],
        fontName="GymTrack",
        fontSize=12,
        leading=18,
        textColor=colors.HexColor("#5D6973"),
        alignment=TA_CENTER,
    )
    heading = ParagraphStyle(
        "Heading",
        parent=styles["Heading1"],
        fontName="GymTrackBold",
        fontSize=20,
        leading=24,
        textColor=colors.HexColor("#111820"),
        spaceAfter=10,
    )
    body = ParagraphStyle(
        "Body",
        parent=styles["BodyText"],
        fontName="GymTrack",
        fontSize=10.5,
        leading=15,
        textColor=colors.HexColor("#27323A"),
        spaceAfter=8,
    )
    caption = ParagraphStyle(
        "Caption",
        parent=body,
        fontSize=9.5,
        leading=14,
        textColor=colors.HexColor("#5D6973"),
        alignment=TA_CENTER,
    )

    document = SimpleDocTemplate(
        str(OUTPUT),
        pagesize=A4,
        rightMargin=16 * mm,
        leftMargin=16 * mm,
        topMargin=17 * mm,
        bottomMargin=17 * mm,
        title="GymTrack — Relatório Final",
        author="Lucas Marcelo Nagel e Pedro Santin",
        subject="Trabalho Final — Desenvolvimento Mobile",
    )

    story = [
        Spacer(1, 35 * mm),
        Paragraph("GYMTRACK", title),
        Paragraph("Relatório final do aplicativo mobile", subtitle),
        Spacer(1, 14 * mm),
        Table(
            [
                ["Autores", "Lucas Marcelo Nagel e Pedro Santin"],
                ["Tecnologia", ".NET MAUI, C#, XAML, MVVM e SQLite"],
                ["Plataforma validada", "Android"],
                ["Versão", "1.0"],
            ],
            colWidths=[48 * mm, 112 * mm],
            style=TableStyle(
                [
                    ("FONTNAME", (0, 0), (-1, -1), "GymTrack"),
                    ("FONTSIZE", (0, 0), (-1, -1), 10),
                    ("TEXTCOLOR", (0, 0), (0, -1), colors.HexColor("#5D6973")),
                    ("TEXTCOLOR", (1, 0), (1, -1), colors.HexColor("#111820")),
                    ("GRID", (0, 0), (-1, -1), 0.5, colors.HexColor("#D4DDE1")),
                    ("BACKGROUND", (0, 0), (-1, -1), colors.white),
                    ("VALIGN", (0, 0), (-1, -1), "MIDDLE"),
                    ("TOPPADDING", (0, 0), (-1, -1), 8),
                    ("BOTTOMPADDING", (0, 0), (-1, -1), 8),
                ]
            ),
        ),
        Spacer(1, 16 * mm),
        Paragraph(
            "Aplicativo offline-first para organizar treinos de musculação, registrar séries e acompanhar a evolução com dados persistidos localmente.",
            subtitle,
        ),
        PageBreak(),
        Paragraph("1. Sistema implementado", heading),
        Paragraph(
            "O GymTrack permite cadastrar e editar exercícios, montar treinos com ordem e parâmetros planejados, executar sessões com cronômetro, registrar carga e repetições, consultar o histórico e visualizar gráficos de maior carga ou volume.",
            body,
        ),
        Paragraph(
            "O aplicativo funciona sem conexão com a internet. Os dados sobrevivem ao reinício porque são armazenados em SQLite no diretório de dados da aplicação.",
            body,
        ),
        Paragraph("2. Arquitetura", heading),
        Paragraph(
            "A interface foi construída em XAML e as regras de apresentação ficam em ViewModels usando CommunityToolkit.Mvvm. A persistência é isolada em DAOs explícitos; nenhuma ViewModel executa SQL. O AnalyticsService calcula a maior carga e o volume por sessão a partir dos registros persistidos.",
            body,
        ),
        Table(
            [
                ["Camada", "Responsabilidade"],
                ["Views", "Telas XAML, navegação e eventos estritamente visuais"],
                ["ViewModels", "Estado, comandos, validações e coordenação dos casos de uso"],
                ["Models", "Entidades persistidas e objetos de consulta"],
                ["DAOs", "Acesso ao SQLite, consultas e transações"],
                ["Services", "Notificações, diálogos e cálculos de evolução"],
            ],
            colWidths=[38 * mm, 122 * mm],
            style=TableStyle(
                [
                    ("FONTNAME", (0, 0), (-1, -1), "GymTrack"),
                    ("FONTNAME", (0, 0), (-1, 0), "GymTrackBold"),
                    ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#23C6A5")),
                    ("TEXTCOLOR", (0, 0), (-1, 0), colors.HexColor("#0E1116")),
                    ("GRID", (0, 0), (-1, -1), 0.5, colors.HexColor("#D4DDE1")),
                    ("FONTSIZE", (0, 0), (-1, -1), 9.5),
                    ("VALIGN", (0, 0), (-1, -1), "TOP"),
                    ("TOPPADDING", (0, 0), (-1, -1), 7),
                    ("BOTTOMPADDING", (0, 0), (-1, -1), 7),
                ]
            ),
        ),
        Spacer(1, 8 * mm),
        Paragraph("3. Banco de dados", heading),
        Paragraph(
            "O banco contém cinco tabelas relacionadas: Exercise, Workout, WorkoutExercise, WorkoutSession e SetRecord. Há campos de texto, números, datas e booleano, chaves estrangeiras, índices e proteção do histórico ao arquivar registros referenciados.",
            body,
        ),
        PageBreak(),
        Paragraph("4. Validação e testes", heading),
        Paragraph(
            "O fluxo principal foi executado no emulador Android: criação e edição de exercícios, composição de treino, execução e retomada de sessão, conclusão de série, finalização, filtros do histórico, detalhe e alternância do gráfico. Também foi confirmado que os dados persistem após encerrar e abrir novamente o aplicativo.",
            body,
        ),
        Table(
            [
                ["Verificação", "Resultado"],
                ["Build Android", "Aprovado, 0 erros"],
                ["Testes automatizados", "68 de 68 aprovados"],
                ["Persistência SQLite", "Aprovada após reinício"],
                ["Navegação", "FlyoutPage, TabbedPage, NavigationPage e modal aprovados"],
                ["Histórico", "ListView, filtros e detalhe aprovados"],
                ["Evolução", "Maior carga e volume com dados reais aprovados"],
            ],
            colWidths=[60 * mm, 100 * mm],
            style=TableStyle(
                [
                    ("FONTNAME", (0, 0), (-1, -1), "GymTrack"),
                    ("FONTNAME", (0, 0), (-1, 0), "GymTrackBold"),
                    ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#23C6A5")),
                    ("GRID", (0, 0), (-1, -1), 0.5, colors.HexColor("#D4DDE1")),
                    ("FONTSIZE", (0, 0), (-1, -1), 9.5),
                    ("TOPPADDING", (0, 0), (-1, -1), 7),
                    ("BOTTOMPADDING", (0, 0), (-1, -1), 7),
                ]
            ),
        ),
        Spacer(1, 10 * mm),
        Paragraph("5. Como executar", heading),
        Paragraph(
            "No Visual Studio Community, instale a carga de trabalho de desenvolvimento de aplicativos multiplataforma .NET, execute Abrir-GymTrack.cmd, selecione um emulador Android e inicie o projeto GymTrack. Pelo terminal, os comandos principais estão documentados no README.md.",
            body,
        ),
        PageBreak(),
        Paragraph("6. Telas do aplicativo", heading),
        Paragraph(
            "As capturas abaixo foram obtidas do aplicativo executado no emulador Android. Cada tela corresponde a uma funcionalidade presente no código entregue.",
            body,
        ),
        PageBreak(),
    ]

    for index, (screen_title, path, description) in enumerate(SCREENS, start=1):
        screen_image = scaled_image(path, 110 * mm, 205 * mm)
        story.append(
            KeepTogether(
                [
                    Paragraph(f"6.{index} {screen_title}", heading),
                    Paragraph(description, caption),
                    Spacer(1, 4 * mm),
                    Table([[screen_image]], hAlign="CENTER"),
                ]
            )
        )
        if index != len(SCREENS):
            story.append(PageBreak())

    document.build(story, onFirstPage=add_page_number, onLaterPages=add_page_number)
    print(OUTPUT)


if __name__ == "__main__":
    main()

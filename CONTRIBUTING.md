<details open>
<summary>English</summary>

# Contributing

Thanks for your interest in contributing.

This project accepts fixes and improvements through pull requests, but I prefer to keep the scope under control. If you want to propose something larger, open an issue first or explain the idea in the PR so we can align on direction.

## Before Submitting

- keep the interface and behavior consistent with what already exists
- avoid adding dependencies unless there is a real need
- prefer small, focused changes
- always validate in `Release`

## Local Validation

Expected flow:

```powershell
dotnet build -c Release
```

If the application is running, close it before rebuilding.

## Pull Requests

A good PR usually does the following:

- explains the problem briefly
- describes what changed
- avoids mixing broad refactors with functional fixes
- includes simple validation steps when useful

## Style

- simple, readable C#
- direct naming
- comments only when they actually help
- no unnecessary cosmetic-only changes

## Contribution Terms

By submitting any contribution to this project, including code, documentation, assets, translations, or tests, you state and agree that:

1. You wrote the original contribution or otherwise have sufficient rights to submit it under these terms.
2. The contribution may be distributed under the project's license.
3. You grant the current maintainer and future maintainers of this repository a worldwide, irrevocable, perpetual, non-exclusive, royalty-free license to use, copy, modify, publish, distribute, sublicense, and relicense your contribution as part of this project.
4. You retain copyright to your contribution, but you grant the rights needed for the project to be maintained, redistributed, and, if necessary, relicensed in the future.
5. You understand that the contribution is provided without warranty of any kind.

If your PR does not include confirmation of these terms, I may ask for it before reviewing or merging.

Use this sentence in the PR description:

`I have read and agree to the contribution terms in CONTRIBUTING.md.`

If you prefer, acceptance can also be given in a public comment on the PR itself.

</details>

<details>
<summary>Português (Brasil)</summary>

# Contribuindo

Obrigado por querer contribuir.

Este projeto aceita correções e melhorias por pull request, mas eu prefiro manter o escopo bem controlado. Se você quiser propor algo maior, abra uma issue antes ou explique a ideia no PR para alinharmos a direção.

## Antes de Enviar

- mantenha a interface e o comportamento consistentes com o que já existe
- evite adicionar dependências sem necessidade real
- prefira mudanças pequenas e objetivas
- sempre valide em `Release`

## Validação Local

Fluxo esperado:

```powershell
dotnet build -c Release
```

Se a aplicação estiver rodando, encerre antes do rebuild.

## Pull Requests

Um bom PR normalmente faz o seguinte:

- explica o problema de forma breve
- descreve o que mudou
- evita misturar refactors amplos com ajustes funcionais
- inclui passos simples de validação quando fizer sentido

## Estilo

- C# simples e legível
- nomes diretos
- comentários apenas quando realmente ajudam
- sem mudanças cosméticas desnecessárias

## Termos de Contribuição

Ao enviar qualquer contribuição para este projeto, inclusive código, documentação, assets, traduções ou testes, você declara e concorda com o seguinte:

1. Você escreveu a contribuição original ou tem direito suficiente para enviá-la sob estes termos.
2. A contribuição pode ser distribuída sob a licença do projeto.
3. Você concede ao mantenedor atual do projeto e a futuros mantenedores deste repositório uma licença mundial, irrevogável, perpétua, não exclusiva e livre de royalties para usar, copiar, modificar, publicar, distribuir, sublicenciar e relicenciar sua contribuição como parte deste projeto.
4. Você mantém a titularidade autoral da sua contribuição, mas concede os direitos necessários para que o projeto possa ser mantido, redistribuído e, se necessário, relicenciado no futuro.
5. Você entende que a contribuição é fornecida sem garantia de qualquer tipo.

Se o seu PR não incluir a confirmação desses termos, eu posso pedir esse aceite antes de revisar ou mesclar.

Use esta frase na descrição do PR:

`I have read and agree to the contribution terms in CONTRIBUTING.md.`

Se preferir, o aceite também pode ser feito em comentário público no próprio PR.

</details>

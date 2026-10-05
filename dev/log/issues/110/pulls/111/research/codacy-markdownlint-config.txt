import {Codacyrc, Pattern} from "codacy-seed"
import {glob} from "glob"
import _ from "lodash"
import {Configuration, Options} from "markdownlint"
import { promises as fs } from "fs"
import { join } from "path"
import yaml from "js-yaml"

import {debug} from "./logging.js"

function patternsToRules (patterns: Pattern[]): Configuration {
  const rules = patterns.map((pattern) => {
    return [
      pattern.patternId,
      pattern.parameters
        ? _.fromPairs(pattern.parameters.map((p) => [p.name, p.value]))
        : true
    ]
  })
  rules.unshift(["default", false])
  return _.fromPairs(rules)
}


async function generateMarkdownlintOptions(
  codacyrc?: Codacyrc
): Promise<Configuration | undefined> {
  if (codacyrc?.tools?.[0]?.patterns && codacyrc.tools[0].patterns.length) {
    return patternsToRules(codacyrc.tools[0].patterns)
  }

  const configFiles = [
    ".markdownlint.json",
    ".markdownlint.yaml",
    ".markdownlint.yml",
    ".markdownlint.jsonc"
  ]

  for (const file of configFiles) {
    try {
      const configPath = join(process.cwd(), file)
      const content = await fs.readFile(configPath, "utf-8")

      if (file.endsWith(".json") || file.endsWith(".jsonc")) {
        return JSON.parse(content)
      } else { 
        return yaml.load(content) as Configuration
      }
    } catch (e: any) {
      if (e.code !== "ENOENT") {
        console.error('Error reading %file:', file, e)
      }
    }
  }

  // If no config file found
  debug("No markdownlint configuration file found")
  return undefined
}

async function generateFilesToAnalyze (
  codacyrc?: Codacyrc
): Promise<string[]> {
  debug("files: creating")

  const files = codacyrc?.files && codacyrc.files.length
    ? codacyrc.files
    : await glob("**/*.md")

  debug("files: finished")
  return files
}

export async function configCreator (codacyrc?: Codacyrc): Promise<Options> {
  debug("config: creating")

  const configuration = await generateMarkdownlintOptions(codacyrc)
  const files = await generateFilesToAnalyze(codacyrc)
  const options: Options = {
    "files": files,
    "config": configuration
  }

  debug(options)
  debug("config: finished")
  return options
}

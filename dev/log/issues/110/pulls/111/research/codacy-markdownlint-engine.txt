import {Codacyrc, Engine, ToolResult} from "codacy-seed"
import {readFile} from "codacy-seed"
import { lint } from "markdownlint/promise"

import {configCreator} from "./configCreator.js"
import {convertResults} from "./convertResults.js"
import {debug} from "./logging.js"

export const engineImpl: Engine = async function (
  codacyrc?: Codacyrc
): Promise<ToolResult[]> {
  const options = await configCreator(codacyrc)

  const markdownlintResults = await lint(options)

  debug(markdownlintResults)

  const files = await Promise.all(
    codacyrc?.files?.map(async (file) => {
      const fileContent = await readFile(file)
      return [file, fileContent.toString()]
    }) || []
  )

  return convertResults(
    markdownlintResults,
    Object.fromEntries(files)
  )
}
